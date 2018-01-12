using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// http://docs.autodesk.com/MAP/2014/CHS/index.html?url=filesMAPLRN/GUID-1C93E885-3623-4969-8374-5E6FD572BB07.htm,topicNumber=MAPLRNd30e135,hash=GUID-EEE3FBB2-8710-45DC-BF98-01AEA2D016AC
    /// http://knowledge.autodesk.com/support/autocad-map-3d/learn-explore/caas/documentation/MAP/2014/ENU/filesMAPUSE/GUID-35F925D0-F768-4186-9D0A-8B2218578808-htm.html
    /// Undershoots are often caused by inaccurate digitizing or when converting scanned data. 
    /// Using the Extend Undershoots cleanup action, you can locate objects that come within 
    /// the specified tolerance radius of each other, but do not meet.
    /// </summary>
    public class ExtendUnderShoots : AlgorithmWithEditor
    {
        private double _tolerance;

        private IEnumerable<IntersectionInfo> _underShootInfos;
        public IEnumerable<IntersectionInfo> UnderShootInfos
        {
            get { return _underShootInfos; }
        }

        public ExtendUnderShoots(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();

            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Search dangling vertices.
                //var danglingSearcher = new DanglingVertexSearcher(objIds, true, transaction);
                //danglingSearcher.SelectCurves = this.SelectCurvesAtPoint;
                //var danglingVertices = danglingSearcher.Search();

                var danglingSearcher = new KdTreeDanglingVertexSearcher(selectedObjectIds, true, transaction);
                var danglingVertices = danglingSearcher.Search();

                // Traverse all dangling vertices and search undershoots
                _underShootInfos = GetUnderShootIntersections(danglingVertices, transaction, _tolerance);
                
                transaction.Commit();
            }

            // the code that you want to measure comes here
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            //Editor.WriteMessage("\n查找未及点花费时间{0}毫秒", elapsedMs);
        }

        public Dictionary<ObjectId, List<ObjectId>> Fix(bool breakTarget)
        {
            if (UnderShootInfos == null || !UnderShootInfos.Any())
                return new Dictionary<ObjectId, List<ObjectId>>();

            var result = new Dictionary<ObjectId, List<ObjectId>>();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Group by target Id, and split it in one time.
                var groups = UnderShootInfos.GroupBy(it => it.TargetId);
                foreach (var group in groups)
                {
                    // Extend source curves first.
                    foreach (var intersectionInfo in group)
                    {
                        CurveUtils.ExtendCurve(intersectionInfo.SourceId, intersectionInfo.IntersectPoint, intersectionInfo.SourceExtendType, transaction);
                    }

                    if (breakTarget)
                    {
                        // Then split the target
                        var points = group.Select(it => it.IntersectPoint);

                        var spliltCurves = CurveUtils.SplitCurve(group.Key, points.ToArray(), transaction);

                        if (spliltCurves != null && spliltCurves.Count > 0)
                        {
                            // The splitted curves has the same layer with original curve, 
                            // so we needn't set its layer explicitly.
                            var ids = new List<ObjectId>();
                            foreach (Curve splitCurve in spliltCurves)
                            {
                                var id = modelSpace.AppendEntity(splitCurve);
                                transaction.AddNewlyCreatedDBObject(splitCurve, true);
                                ids.Add(id);
                            }
                            result.Add(group.Key, ids);

                            // Erase the original one
                            var originCurve = (Entity)transaction.GetObject(group.Key, OpenMode.ForRead) as Curve;
                            if (originCurve != null)
                            {
                                originCurve.UpgradeOpen();
                                originCurve.Erase();
                            }
                        }
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        private IEnumerable<IntersectionInfo> GetUnderShootIntersections(IEnumerable<CurveVertex> danglingVertices, Transaction transaction, double tolerance)
        {
            var result = new List<IntersectionInfo>();
            foreach (var vertex in danglingVertices)
            {
                var info = GetUnderShootIntersection(vertex, transaction, tolerance);
                if (info != null)
                { 
                    result.Add(info);
                }
            }
            return result;
        }

        private IntersectionInfo GetUnderShootIntersection(CurveVertex vertex, Transaction transaction, double tolerance)
        {
            var curve = transaction.GetObject(vertex.Id, OpenMode.ForRead);
            var desiredExtend = CurveUtils.GetExtendType((Curve)curve, vertex.Point);
            
            var line = curve as Line;
            var arc = curve as Arc;
            var polyline = curve as Polyline;
            var polyline2d = curve as Polyline2d;
            if (polyline != null)
            {
                line = CurveUtils.CutOutLineFromPolyLine(polyline, vertex.Point);
            }
            else if (polyline2d != null)
            {
                line = CurveUtils.CutOutLineFromPolyline2d(polyline2d, vertex.Point, transaction);
            }

            // Construct a polygon to get adjacent entities.
            Point3dCollection polygon = null;
            if (line != null)
            {
                var direction = line.EndPoint - line.StartPoint;
                if (desiredExtend == ExtendType.ExtendStart)
                    direction = -1.0*direction;
                polygon = BuildSelectionPolygonForLine(vertex.Point, direction, tolerance);
            }
            else if (arc != null)
            {
                polygon = BuildSelectionPolygonForArc(vertex.Point, tolerance);
            }

            // Zoom window
            var minPt = new Point3d(vertex.Point.X - tolerance, vertex.Point.Y - tolerance, 0.0);
            var maxPt = new Point3d(vertex.Point.X + tolerance, vertex.Point.Y + tolerance, 0.0);
            var extents = new Extents3d(minPt, maxPt);
            Editor.ZoomToWin(extents, factor: 2.0);

            var selectionResult = Editor.SelectCrossingPolygon(polygon, SelectionFilterUtils.OnlySelectCurve());
            if (selectionResult.Status != PromptStatus.OK || selectionResult.Value == null)
                return null;

            // Check each adjacent entity whether they are intersected.
            var intersectionInfos = new List<IntersectionInfo>();
            var adjacentIds = selectionResult.Value.GetObjectIds();
            foreach (ObjectId objId in adjacentIds)
            {
                var targetCurve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                if (targetCurve == null)
                    continue;

                // Calculate the intersection
                IntersectionInfo info = null;
                if (line != null)
                    info = IntersectLineAndCurve(line, targetCurve, vertex.Point, desiredExtend);
                else if (arc != null)
                    info = IntersectArcAndCurve(arc, targetCurve, vertex.Point, desiredExtend);

                if (info != null && info.SourceExtendType == desiredExtend)
                {
                    info.SourceId = vertex.Id;
                    info.TargetId = objId;

                    intersectionInfos.Add(info);
                }
            }

            // Get nearest point.
            if (intersectionInfos.Count <= 0)
                return null;

            var nearest = intersectionInfos[0];
            var nearsetDist = (intersectionInfos[0].IntersectPoint - vertex.Point).Length;
            for (int i = 1; i < intersectionInfos.Count; i++)
            {
                var dist = (intersectionInfos[i].IntersectPoint - vertex.Point).Length;
                if (dist < nearsetDist)
                {
                    nearsetDist = dist;
                    nearest = intersectionInfos[i];
                }
            }

            if (nearsetDist.Larger(tolerance))
                return null;

            return nearest;
        }

        private Point3dCollection BuildSelectionPolygonForLine(Point3d point, Vector3d direction, double tolerance)
        {
            var result = new Point3dCollection();
            var normalDir = direction.GetNormal();
            var perpDir = normalDir.CrossProduct(Vector3d.ZAxis).GetNormal();
            var width = 0.1;

            //  B________________C
            // |                 |
            // A----------------------> Direction
            // |_________________|D
            //  E
            var pointB = point + perpDir*width;
            var pointC = pointB + normalDir*tolerance;
            var pointD = pointC + perpDir*(-2*width);
            var pointE = pointD + normalDir*(-tolerance);

            result.Add(pointB);
            result.Add(pointC);
            result.Add(pointD);
            result.Add(pointE);

            return result;
        }

        private Point3dCollection BuildSelectionPolygonForArc(Point3d point, double tolerance)
        {
            var result = new Point3dCollection();
            // B ____________C
            // |             |
            // |             |
            // |             |
            // |             |
            // |_____________|
            // E             D
            var pointB = new Point3d(point.X - tolerance, point.Y + tolerance, point.Z);
            var pointC = new Point3d(point.X + tolerance, point.Y + tolerance, point.Z);
            var pointD = new Point3d(point.X + tolerance, point.Y - tolerance, point.Z);
            var pointE = new Point3d(point.X - tolerance, point.Y - tolerance, point.Z);
            result.Add(pointB);
            result.Add(pointC);
            result.Add(pointD);
            result.Add(pointE);
            return result;
        }

        private IntersectionInfo IntersectLineAndCurve(Line line, Curve curve, Point3d sorucePoint, ExtendType desireExtendType)
        {
            var points = new Point3dCollection();
            line.IntersectWith(curve, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count == 0)
                return null;

            // NOTE: Use Line's GetParameterAtPoint will throw exception if the intersect point is 
            // on the line's extension, but LineSegment3d.GetParameterOf is available, so I convert
            // the Line to LineSegment3d here.
            var lineSegment = new LineSegment3d(line.StartPoint, line.EndPoint);
            Point3d? nearestPoint = null;
            double? nearestDist = null;
            foreach (Point3d point in points)
            {
                var param = lineSegment.GetParameterOf(point);
                var extendType = CurveIntersectUtils.ParamToExtendTypeForLine(param);
                if (extendType != desireExtendType)
                    continue;

                var dist = (point - sorucePoint).LengthSqrd;
                if (nearestDist == null || dist < nearestDist.Value)
                {
                    nearestDist = dist;
                    nearestPoint = point;
                }
            }

            IntersectionInfo result = null;
            if (nearestPoint != null)
                result = new IntersectionInfo(desireExtendType, ExtendType.None, nearestPoint.Value);
            return result;
        }

        private IntersectionInfo IntersectArcAndCurve(Arc arc, Curve curve, Point3d point, ExtendType? coerceArcExtendType)
        {
            var points = new Point3dCollection();
            arc.IntersectWith(curve, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count <= 0)
                return null;

            // Get the nearest point
            var nearsetPoint = points[0];
            var dist = (point - nearsetPoint).LengthSqrd;
            for(int i = 1; i < points.Count; i++)
            {
                var newDist = (points[i] - point).LengthSqrd;
                if (newDist < dist)
                {
                    nearsetPoint = points[i];
                    dist = newDist;
                }
            }
 
            // Calculate extend type
            if (coerceArcExtendType == null)
            {
                var circularArc = new CircularArc3d(arc.Center, arc.Normal, arc.Normal.GetPerpendicularVector(), arc.Radius,
                    arc.StartAngle, arc.EndAngle);
                var arcParam = circularArc.GetParameterOf(nearsetPoint);
                coerceArcExtendType = CurveIntersectUtils.ParamToExtendTypeForArc(circularArc, arcParam, null);
            }

            var result = new IntersectionInfo(coerceArcExtendType.Value, ExtendType.None, nearsetPoint);
            return result;
        }
    }
}
