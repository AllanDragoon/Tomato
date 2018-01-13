using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using ClipperLib;
using LS.MapClean.Addin.Utils;
using TopologyTools.Utils;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace LS.MapClean.Addin.Algorithms
{
    public struct PolygonIntersect
    {
        /// <summary>
        /// Source polygon Id.
        /// </summary>
        public ObjectId SourceId { get; set; }

        /// <summary>
        /// Target polygon Id.
        /// </summary>
        public ObjectId TargetId { get; set; }

        ///// <summary>
        ///// Intersection paths.
        ///// </summary>
        //public List<Point3d[]> IntersectionPaths { get; set; }

        public List<Polyline> Intersections { get; set; }

        ///// <summary>
        ///// Source intersection points.
        ///// </summary>
        //public Point3d[] SourceIntersectPoints { get; set; }

        ///// <summary>
        ///// Target intersect points.
        ///// </summary>
        //public Point3d[] TargetIntersectPoints { get; set; }
    }

    public class PolygonIntersectSearcher : AlgorithmWithEditor
    {
        private IEnumerable<PolygonIntersect> _intersects = new PolygonIntersect[0];
        public IEnumerable<PolygonIntersect> Intersects
        {
            get { return _intersects; }
        }

        /// <summary>
        /// 是否排除包含的情况，默认是true
        /// </summary>
        public bool ExceptInclude { get; private set; }

        /// <summary>
        /// 相交计算时，如果TargetAreaRatio不为空，那么需要把面积比例考虑进去，小于此面积比例的不予考虑
        /// </summary>
        public double? TargetAreaRatio { get; private set; }

        public PolygonIntersectSearcher(Editor editor, double? targetAreaRatio, bool exceptInclude = true)
            : base(editor)
        {
            ExceptInclude = exceptInclude;
            TargetAreaRatio = targetAreaRatio;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            Check(selectedObjectIds, selectedObjectIds);
        }

        /// <summary>
        /// 检查和sourceIds相交的objectIds
        /// </summary>
        /// <param name="objectIds"></param>
        /// <param name="sourceIds"></param>
        public void Check(IEnumerable<ObjectId> objectIds, IEnumerable<ObjectId> sourceIds)
        {
            if (!objectIds.Any() || !sourceIds.Any())
                return;

            var database = Editor.Document.Database;
            // Build a kd tree for searching intersection
            var allVertices = new List<CurveVertex>();
            var closedSourceIds = new List<ObjectId>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in objectIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    allVertices.AddRange(vertices.Select(it => new CurveVertex(it, objectId)));
                    curve.Dispose();
                }

                foreach (var sourceId in sourceIds)
                {
                    var curve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    closedSourceIds.Add(sourceId);
                    curve.Dispose();
                }
                transaction.Commit();
            }

            // Create a kdTree
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);

            // Use kd tree to check intersect.
            var intersects = new List<PolygonIntersect>();
            var analyzed = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in closedSourceIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    var extents = curve.GeometricExtents;
                    var nearVertices = kdTree.BoxedRange(extents.MinPoint.ToArray(), extents.MaxPoint.ToArray());

                    foreach (var curveVertex in nearVertices)
                    {
                        if (curveVertex.Id == objectId ||
                            analyzed.Contains(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id)))
                        {
                            continue;
                        }

                        analyzed.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        var polygonIntersect = AnalyzePolygonIntersection(objectId, curveVertex.Id, transaction);
                        if (polygonIntersect != null)
                        {
                            var sourceId = polygonIntersect.Value.SourceId;
                            var targetId = polygonIntersect.Value.TargetId;
                            var existing = intersects.FirstOrDefault(it => it.SourceId == sourceId && it.TargetId == targetId ||
                                                                     it.SourceId == targetId && it.TargetId == sourceId);
                            if (existing.Equals(default(PolygonIntersect)))
                                intersects.Add(polygonIntersect.Value);
                        }
                    }
                }
                transaction.Commit();
            }
            _intersects = intersects;
        }

        private Polyline CreatePolygon(Point3d[] points)
        {
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            polyline.Closed = true;
            return polyline;
        }

        private PolygonIntersect? AnalyzePolygonIntersection(ObjectId sourceId, ObjectId targetId, Transaction transaction)
        {
            var sourceCurve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
            var targetCurve = transaction.GetObject(targetId, OpenMode.ForRead) as Curve;
            if (!IsCurveClosed(sourceCurve) || !IsCurveClosed(targetCurve))
                return null;
            
            // Use clipper to calculate the intersection
            var precision = 0.000001;
            var subject = new List<List<IntPoint>>(1);
            var clipper = new List<List<IntPoint>>(1);
            var result = new List<List<IntPoint>>();

            var sourceVertices = CurveUtils.GetDistinctVertices(sourceCurve, transaction);
            var targetVertices = CurveUtils.GetDistinctVertices(targetCurve, transaction);

            var subjectPath = sourceVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            var clipperPath = targetVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            subject.Add(subjectPath);
            clipper.Add(clipperPath);
            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            cpr.Execute(ClipType.ctIntersection, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            if (result.Count <= 0)
            {
                //Editor.WriteMessage("有相交，但Clipper没有计算出相交部分");
                return null;
            }

            var intersectPaths = new List<Polyline>();
            foreach (var path in result)
            {
                var points = path.Select(it => new Point3d(it.X * precision, it.Y * precision, 0.0)).ToArray();
                var polyline = CreatePolygon(points);
                // If the polygon's area is very small, just ignore, or it will bother user.
                var intersectArea = polyline.Area;
                if (intersectArea.Smaller(0.001))
                {
                    polyline.Dispose();
                    continue;
                }
                else if (TargetAreaRatio != null)
                {
                    var targetArea = targetCurve.Area;
                    if ((intersectArea/targetArea).Smaller(TargetAreaRatio.Value))
                    {
                        polyline.Dispose();
                        continue;
                    }
                }
                intersectPaths.Add(polyline);
            }

            if (intersectPaths.Count <= 0)
                return null;

            // 将包含的情况排除
            if (intersectPaths.Count == 1)
            {
                var intersectPoints = CurveUtils.GetDistinctVertices(intersectPaths[0], null);
                var qualified = IsIntersectQualified(sourceId, sourceVertices, targetId, targetVertices, intersectPoints, transaction);
                if(!qualified)
                {
                    intersectPaths[0].Dispose();
                    intersectPaths.Clear();
                    return null;
                }
            }

            return new PolygonIntersect()
            {
                SourceId = sourceId,
                TargetId = targetId,
                Intersections = intersectPaths
            };
        }

        protected virtual bool IsIntersectQualified(ObjectId sourceId, List<Point3d> sourceVertices, ObjectId targetId,
            List<Point3d> targetVertices, List<Point3d> intersectPoints, Transaction transaction)
        {
            // 是否将包含的情况排除
            if (ExceptInclude)
            {
                var duplicateWithSource = PolygonIncludeSearcher.AreDuplicateEntities(sourceVertices, intersectPoints);
                var duplicateWithTarget = PolygonIncludeSearcher.AreDuplicateEntities(targetVertices, intersectPoints);
                if (duplicateWithSource && !duplicateWithTarget || !duplicateWithSource && duplicateWithTarget)
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsCurveClosed(Curve curve)
        {
            bool closed = false;
            var polyline = curve as Polyline;
            var polyline2d = curve as Polyline2d;
            if (polyline != null)
                closed = polyline.Closed;
            else if (polyline2d != null)
                closed = polyline2d.Closed;
            return closed;
        }
    }

    public class PolygonIntersectWithoutHoleSearcher : PolygonIntersectSearcher
    {
        public PolygonIntersectWithoutHoleSearcher(Editor editor)
            : base(editor, null, exceptInclude:false)
        {
        }
        protected override bool IsIntersectQualified(ObjectId sourceId, List<Point3d> sourceVertices, ObjectId targetId,
            List<Point3d> targetVertices, List<Point3d> intersectPoints, Transaction transaction)
        {
            var duplicateWithSource = PolygonIncludeSearcher.AreDuplicateEntities(sourceVertices, intersectPoints);
            var duplicateWithTarget = PolygonIncludeSearcher.AreDuplicateEntities(targetVertices, intersectPoints);
            // 确保它们不是软件认为的孔洞
            if (duplicateWithSource && !duplicateWithTarget && PolygonHoleHelper.IsHoleReferenced(transaction, sourceId) 
                || !duplicateWithSource && duplicateWithTarget && PolygonHoleHelper.IsHoleReferenced(transaction, targetId))
            {
                return false;
            }
            return true;
        }
    }

    public class PolygonDuplicateSearcher : PolygonIntersectSearcher
    {
        public PolygonDuplicateSearcher(Editor editor)
            : base(editor, null)
        {
        }

        protected override bool IsIntersectQualified(ObjectId sourceId, List<Point3d> sourceVertices, ObjectId targetId,
            List<Point3d> targetVertices, List<Point3d> intersectPoints, Transaction transaction)
        {
            var duplicate = PolygonIncludeSearcher.AreDuplicateEntities(sourceVertices, targetVertices);
            if (duplicate)
                return true;
            return false;
        }
    }

}
