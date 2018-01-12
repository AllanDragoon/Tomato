using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms.VisualStyles;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;
using TopologyTools.ConvexHull;

namespace LS.MapClean.Addin.Algorithms
{
    public static class CurveUtils
    {
        /// <summary>
        /// Get curve's end points
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Point3d[] GetCurveEndPoints(ObjectId id, Transaction transaction)
        {
            var result = new Point3d[0];
            var dbObj = transaction.GetObject(id, OpenMode.ForRead);

            var curve = dbObj as Curve;
            // Line, Polyline, Arc
            // TODO: Circle, Ellipse?
            if (curve != null) // Line
            {
                result = new Point3d[] { curve.StartPoint, curve.EndPoint };
            }
            return result;
        }

        /// <summary>
        /// Extend a curve to the targetPoint.
        /// </summary>
        /// <param name="curveId"></param>
        /// <param name="targetPoint"></param>
        /// <param name="extendType"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static bool ExtendCurve(ObjectId curveId, Point3d targetPoint, ExtendType extendType, Transaction transaction)
        {
            bool result = false;
            var curve = (Entity) transaction.GetObject(curveId, OpenMode.ForWrite);
            if (curve is Line)
            {
                var line = (Line)curve;
                if (extendType == ExtendType.ExtendStart)
                    line.StartPoint = targetPoint;
                else if (extendType == ExtendType.ExtendEnd)
                    line.EndPoint = targetPoint;
                result = true;
            }
            else if (curve is Polyline)
            {
                var polyline = (Polyline)curve;
                if (extendType == ExtendType.ExtendStart)
                    polyline.SetPointAt(0, new Point2d(targetPoint.X, targetPoint.Y));
                else if (extendType == ExtendType.ExtendEnd)
                    polyline.SetPointAt(polyline.NumberOfVertices - 1, new Point2d(targetPoint.X, targetPoint.Y));
                result = true;
            }
            else if (curve is Polyline2d)
            {
                var polyline2d = (Polyline2d)curve;
                Vertex2d vertex = null;
                if (extendType == ExtendType.ExtendStart)
                {
                    foreach (ObjectId vertexId in polyline2d)
                    {
                        vertex = transaction.GetObject(vertexId, OpenMode.ForWrite) as Vertex2d;
                    }
                }
                else if (extendType == ExtendType.ExtendEnd)
                {
                    var enumerator = polyline2d.GetEnumerator();
                    var lastVertexId = ObjectId.Null;
                    while (enumerator.MoveNext())
                    {
                        lastVertexId = (ObjectId)enumerator.Current;
                    }
                    vertex = transaction.GetObject(lastVertexId, OpenMode.ForWrite) as Vertex2d;
                }

                if (vertex != null)
                    vertex.Position = targetPoint;
                result = true;
            }
            else if (curve is Arc)
            {
                // TODO:
            }

            curve.Dispose();
            return result;
        }

        /// <summary>
        /// Cut out a line from polyline by point.
        /// </summary>
        /// <param name="polyline"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Line CutOutLineFromPolyLine(Polyline polyline, Point3d point)
        {
            Line result = null;
            if (polyline.EndPoint == point)
            {
                var startPoint = polyline.GetPoint3dAt(polyline.NumberOfVertices - 2);
                result = new Line(startPoint, point);
            }
            else
            {
                result = new Line(point, polyline.GetPoint3dAt(1));
            }
            return result;
        }

        public static Line CutOutLineFromPolyline2d(Polyline2d polyline2d, Point3d point, Transaction transaction)
        {
            Line result = null;
            var vertices = new List<Vertex2d>();
            foreach (ObjectId objId in polyline2d)
            {
                var vertex = transaction.GetObject(objId, OpenMode.ForRead) as Vertex2d;
                vertices.Add(vertex);
            }

            if (polyline2d.EndPoint == point)
            {
                var startPoint = vertices[vertices.Count - 2].Position;
                result = new Line(startPoint, point);
            }
            else
            {
                result = new Line(point, vertices[1].Position);
            }
            return result;
        }

        /// <summary>
        /// Get curve's extend type.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static ExtendType GetExtendType(Curve curve, Point3d point)
        {
            if (curve.StartPoint == point)
                return ExtendType.ExtendStart;
            else
                return ExtendType.ExtendEnd;
        }

        public static IEnumerable<Polyline> MergeCurves(IEnumerable<Curve> curves, Transaction transaction,
            out List<Curve> immergables)
        {
            immergables = new List<Curve>();

            var groups = GroupCurvesForMerge(curves);
            var result = new List<Polyline>();
            foreach (var @group in groups)
            {
                if (@group.Count < 2) { 
                    immergables.AddRange(@group);
                    continue;
                }

                // Merget them
                var polyline = MergeCurves(@group[0], @group[1], transaction);
                var disposedCurves = new List<Polyline>();
                for (int i = 2; i < @group.Count; i++)
                {
                    var previous = polyline;
                    polyline = MergeCurves(previous, @group.ElementAt(i), transaction);
                    if (polyline == null)
                    {
                        polyline = previous;
                        immergables.Add(@group.ElementAt(i));
                        continue;
                    }
                    disposedCurves.Add(previous);
                }
                foreach (var disposedCurve in disposedCurves)
                {
                    disposedCurve.Dispose();
                }
                result.Add(polyline);
                
            }
            return result;
        }

        private static List<List<Curve>> GroupCurvesForMerge(IEnumerable<Curve> curves)
        {
            var groups = new List<List<Curve>>();
            foreach (var curve in curves)
            {
                bool grouped = false;
                foreach (var @group in groups)
                {
                    var existing = @group.FirstOrDefault(it => IsMergable(it, curve));
                    if (existing != null)
                    {
                        @group.Add(curve);
                        grouped = true;
                    }

                }
                if (!grouped)
                {
                    groups.Add(new List<Curve>(){curve});
                }
            }

            return groups;
        }

        private static bool IsMergable(Curve source, Curve target)
        {
            if (source.StartPoint == target.StartPoint ||
                source.StartPoint == target.EndPoint ||
                source.EndPoint == target.StartPoint ||
                source.EndPoint == target.EndPoint)
            {
                return true;
            }

            return false;
        }

        public static Polyline MergeCurves(Curve source, Curve target, Transaction transaction)
        {
            if (!IsMergable(source, target))
                return null;

            var sourcePoints = GetDistinctVertices2D(source, transaction);
            var targetPoints = GetDistinctVertices2D(target, transaction);
            
            var polyline = new Polyline();
            for (int i = 0; i < sourcePoints.Count(); i++)
            {
                polyline.AddVertexAt(i, sourcePoints.ElementAt(i), 0, 0, 0);
            }

            if (source.StartPoint == target.StartPoint)
            {
                foreach (var targetPoint in targetPoints)
                {
                    polyline.AddVertexAt(0, targetPoint, 0, 0, 0);
                }
            }
            else if (source.StartPoint == target.EndPoint)
            {
                targetPoints.Reverse();
                foreach (var targetPoint in targetPoints)
                {
                    polyline.AddVertexAt(0, targetPoint, 0, 0, 0);
                }
            }
            else if (source.EndPoint == target.StartPoint)
            {
                int indexBase = polyline.NumberOfVertices;
                for (int i = 0; i < targetPoints.Count(); i++)
                {
                    polyline.AddVertexAt(indexBase+i, targetPoints.ElementAt(i), 0, 0, 0);
                }
            }
            else if (source.EndPoint == target.EndPoint)
            {
                int indexBase = polyline.NumberOfVertices;
                targetPoints.Reverse();
                for (int i = 0; i < targetPoints.Count(); i++)
                {
                    polyline.AddVertexAt(indexBase + i, targetPoints.ElementAt(i), 0, 0, 0);
                }
            }

            return polyline;
        }

        /// <summary>
        /// Split a curve by a list of points.
        /// </summary>
        /// <param name="curveId"></param>
        /// <param name="splitPoints"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static DBObjectCollection SplitCurve(ObjectId curveId, Point3d[] splitPoints, Transaction transaction)
        {
            var curve = (Entity) transaction.GetObject(curveId, OpenMode.ForRead) as Curve;
            if (curve == null)
                return null;

            var splittedCurves = SplitCurve(curve, splitPoints);
            curve.Dispose();
            return splittedCurves;
        }

        /// <summary>
        /// Split a non-self-intersect curve.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="splitPoints"></param>
        /// <returns></returns>
        public static DBObjectCollection SplitCurve(Curve curve, Point3d[] splitPoints)
        {
            if (curve == null)
                return new DBObjectCollection();

            var sortedDoubles = SortBreakPointsByCurve(curve, splitPoints);
            if (sortedDoubles.Count <= 0)
                return new DBObjectCollection();

            DBObjectCollection result = new DBObjectCollection();
            try
            {
                result = curve.GetSplitCurves(sortedDoubles);
            }
            catch (Exception)
            {
                foreach (double sortedDouble in sortedDoubles)
                {
                    System.Diagnostics.Trace.WriteLine(sortedDouble);
                }
            }
            return result;
        }

        /// <summary>
        /// Split a self intersect curve.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="splitPoints"></param>
        /// <returns></returns>
        public static DBObjectCollection SplitSelfIntersectCurve(Curve curve, Point3d[] splitPoints, Transaction transaction)
        {
            var result = new DBObjectCollection();

            IList<Point3d> subPointList = new List<Point3d>();
            foreach (Point3d point in splitPoints)
            {
                bool isPointOnCurve = GeometryUtils.IsPointOnCurveGCP(curve, point);
                if (isPointOnCurve && !subPointList.Contains(point))
                {
                    // 非起/终点则加到subPointList用来分割curve
                    subPointList.Add(point);
                }
            }

            var startPoint = curve.StartPoint;
            var endPoint = curve.EndPoint;

            //// No need to split again if they are curve's start and end point
            //if (subPointList.Count == 2 &&
            //    (subPointList[0] == startPoint || subPointList[1] == startPoint) &&
            //    (subPointList[0] == endPoint || subPointList[1] == endPoint))
            //{
            //    return result;
            //}

            if (subPointList.Count > 0)
            {
                if (!startPoint.IsEqualTo(endPoint) && 
                    subPointList.Contains(startPoint) && 
                    !subPointList.Contains(endPoint))
                {
                    curve.ReverseCurve();
                }

                var curves = CurveUtils.SplitCurve(curve, subPointList.ToArray());
                // split之后还是一条线,说明不能split了直接加到result里
                if (curves != null && curves.Count == 1)
                {
                    foreach (DBObject dbObj in curves)
                        result.Add(dbObj);
                    return result;
                }

                // 递归找sub split curves.
                foreach (Curve subCurve in curves)
                {
                    if (subCurve == null)
                        continue;

                    // If subcuve is same with curve, just continue.
                    var vertices = GetDistinctVertices(curve, transaction);
                    var subVertices = GetDistinctVertices(subCurve, transaction);

                    bool duplicate = true;
                    if (vertices.Count() != subVertices.Count())
                    {
                        duplicate = false;
                    }
                    else
                    {
                        foreach (var subVertex in subVertices)
                        {
                            if (!vertices.Contains(subVertex))
                            {
                                duplicate = false;
                                break;
                            }
                        }
                    }

                    // If dupliate, just continue.
                    if (duplicate)
                    { 
                        subCurve.Dispose();
                        continue;
                    }

                    var subSplitCurves = SplitSelfIntersectCurve(subCurve, subPointList.ToArray(), transaction);
                    if (subSplitCurves.Count <= 0)
                        result.Add(subCurve);
                    else
                    {
                        foreach (Curve subSplitCurve in subSplitCurves)
                        {
                            result.Add(subSplitCurve);
                        }
                    }
                }
            }
            return result;
        }

        public static DoubleCollection SortBreakPointsByCurve(Curve curve, Point3d[] splitPoints)
        {
            var sortedDoubles = new DoubleCollection();
            foreach (Point3d point in splitPoints)
            {
                double? param = null;
                try
                {
                    param = GeometryUtils.GetPointParameter(curve, point);
                }
                catch
                {
                }
                if (param == null)
                    continue;

                // Let it be 0.0 if it's near 0.0
                if (param.Value.EqualsWithTolerance(0.0))
                    param = 0.0;

                int index = sortedDoubles.Count;
                for (int i = 0; i < sortedDoubles.Count; i++)
                {
                    if (param.Value.Smaller(sortedDoubles[i]))
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= sortedDoubles.Count)
                    sortedDoubles.Add(param.Value);
                else
                    sortedDoubles.Insert(index, param.Value);
            }
            return sortedDoubles;
        }

        public static IEnumerable<LineSegment2d> GetSegment2dsOfCurve(Curve curve, Transaction transaction)
        {
            if (curve is Line)
            {
                var segment = GetSegment2dOfLine((Line)curve);
                return new LineSegment2d[] { segment };
            }
            else if (curve is Polyline)
            {
                var segments = GetSegment2dsOfPolyline((Polyline)curve);
                return segments;
            }
            else if (curve is Polyline2d)
            {
                var segments = GetSegment2dsOfPolyline2D((Polyline2d) curve, transaction);
                return segments;
            }
            return new LineSegment2d[0];
        }

        public static IEnumerable<LineSegment3d> GetSegment3dsOfCurve(Curve curve, Transaction transaction)
        {
            if (curve is Line)
            {
                var segment = GetSegment3dOfLine((Line)curve);
                return new LineSegment3d[] { segment };
            }
            else if (curve is Polyline)
            {
                var segments = GetSegment3dsOfPolyline((Polyline)curve);
                return segments;
            }
            else if (curve is Polyline2d)
            {
                var segments = GetSegment3dsOfPolyline2D((Polyline2d)curve, transaction);
                return segments;
            }
            return new LineSegment3d[0];
        }

        private static LineSegment2d GetSegment2dOfLine(Line line)
        {
            var startPoint = new Point2d(line.StartPoint.X, line.StartPoint.Y);
            var endPoint = new Point2d(line.EndPoint.X, line.EndPoint.Y);
            var segment = new LineSegment2d(startPoint, endPoint);
            return segment;
        }

        private static LineSegment3d GetSegment3dOfLine(Line line)
        {
            var segment = new LineSegment3d(line.StartPoint, line.EndPoint);
            return segment;
        }

        private static IEnumerable<LineSegment2d> GetSegment2dsOfPolyline(Polyline polyline)
        {
            var points = GetDistinctVertices2D(polyline);
            if (polyline.Closed && points[0] != points[points.Count - 1])
                points.Add(points[0]);
            var segments = GenerateLineSegment2ds(points);
            return segments;
        }

        private static IEnumerable<LineSegment3d> GetSegment3dsOfPolyline(Polyline polyline)
        {
            var points = GetDistinctVertices(polyline);
            if (polyline.Closed && points[0] != points[points.Count - 1])
                points.Add(points[0]);
            var segments = GenerateLineSegment3ds(points);
            return segments;
        }

        private static IEnumerable<LineSegment2d> GetSegment2dsOfPolyline2D(Polyline2d polyline2d, Transaction transaction)
        {
            var points = GetDistinctVertices2D(polyline2d, transaction);
            if (polyline2d.Closed && points[0] != points[points.Count -1])
                points.Add(points[0]);
            var segments = GenerateLineSegment2ds(points);
            return segments;
        }

        private static IEnumerable<LineSegment3d> GetSegment3dsOfPolyline2D(Polyline2d polyline2d, Transaction transaction)
        {
            var points = GetDistinctVertices(polyline2d, transaction);
            if (polyline2d.Closed && points[0] != points[points.Count - 1])
                points.Add(points[0]);
            var segments = GenerateLineSegment3ds(points);
            return segments;
        }

        public static IEnumerable<Point3d> GetDistinctVertices(ObjectId curveId)
        {
            using (var transaction = curveId.Database.TransactionManager.StartTransaction())
            {
                var result = GetDistinctVertices(curveId, transaction);
                transaction.Commit();
                return result;
            }
        }

        public static IEnumerable<Point3d> GetDistinctVertices(ObjectId curveId, Transaction transaction)
        {
            var curve = transaction.GetObject(curveId, OpenMode.ForRead) as Curve;
            if (curve == null)
                return new Point3d[0];
            var result = GetDistinctVertices(curve, transaction);
            // Dispose the curve.
            curve.Dispose();
            return result;
        }

        public static IEnumerable<Point2d> GetDistinctVertices2D(ObjectId curveId)
        {
            using (var transaction = curveId.Database.TransactionManager.StartTransaction())
            {
                var result = GetDistinctVertices2D(curveId, transaction);
                transaction.Commit();
                return result;
            }
        }

        public static List<Point2d> GetDistinctVertices2D(ObjectId curveId, Transaction transaction)
        {
            var curve = transaction.GetObject(curveId, OpenMode.ForRead) as Curve;
            if (curve == null)
                return new List<Point2d>();
            var result = GetDistinctVertices2D(curve, transaction);
            // Dispose the curve.
            curve.Dispose();
            return result;
        }

        public static List<Point3d> GetDistinctVertices(Curve curve, Transaction transaction)
        {
            if (curve is Line)
                return GetDistinctVertices((Line) curve);
            else if (curve is Polyline)
                return GetDistinctVertices((Polyline) curve);
            else if (curve is Polyline2d)
                return GetDistinctVertices((Polyline2d) curve, transaction);

            return new List<Point3d>();
        }

        public static List<Point2d> GetDistinctVertices2D(Curve curve, Transaction transaction)
        {
            if (curve is Line)
                return GetDistinctVertices2D((Line)curve);
            else if (curve is Polyline)
                return GetDistinctVertices2D((Polyline)curve);
            else if (curve is Polyline2d)
                return GetDistinctVertices2D((Polyline2d)curve, transaction);

            return new List<Point2d>();
        }

        public static bool IsPolygonClockWise(Polyline polyline)
        {
            var vertices = GetDistinctVertices(polyline);
            // http://dominoc925.blogspot.com/2012/03/c-code-to-determine-if-polygon-vertices.html
            // That has the same vertex for the first and last array members.
            if (vertices[0] != vertices[vertices.Count - 1])
                vertices.Add(vertices[0]);
            var point2ds = vertices.Select(it => new Point2d(it.X, it.Y));
            var clockwise = ComputerGraphics.ClockWise2(point2ds.ToArray());

            return clockwise;
        }

        public static bool IsPolygonClockWise(Polyline2d polyline2d, Transaction transaction)
        {
            var vertices = GetDistinctVertices(polyline2d, transaction);
            // http://dominoc925.blogspot.com/2012/03/c-code-to-determine-if-polygon-vertices.html
            // That has the same vertex for the first and last array members.
            if (vertices[0] != vertices[vertices.Count - 1])
                vertices.Add(vertices[0]);
            var point2ds = vertices.Select(it => new Point2d(it.X, it.Y));
            var clockwise = ComputerGraphics.ClockWise2(point2ds.ToArray());

            return clockwise;
        }

        /// <summary>
        /// Get convex hull of a polygon, the return value is a polyline.
        /// </summary>
        /// <param name="polygonId"></param>
        /// <returns></returns>
        public static Polyline GetConvexHullOfPolygon(ObjectId polygonId)
        {
            Polyline polyline = null;
            using (var transaction = polygonId.Database.TransactionManager.StartTransaction())
            {
                var vertices = CurveUtils.GetDistinctVertices2D(polygonId, transaction);
                if (!vertices.Any())
                    return null;

                var convexHull = new ConvexHull<Point2d>(vertices, p => p.X, p => p.Y,
                    (x, y) => new Point2d(x, y), (a, b) => a == b);
                convexHull.CalcConvexHull();

                var points = convexHull.GetResultsAsArrayOfPoint();

                // Create one polyline
                polyline = new Polyline();
                for (int i = 0; i < points.Length; i++)
                {
                    polyline.AddVertexAt(i, points[i], 0, 0, 0);
                }

                transaction.Commit();
            }
            return polyline;
        }

        /// <summary>
        /// Get convex hull offset of a polygon, the return value is a polyline.
        /// </summary>
        /// <param name="parcelId"></param>
        /// <returns></returns>
        public static Polyline GetConvexHullOffsetOfPolygon(ObjectId parcelId, double offset)
        {
            Polyline polyline = GetConvexHullOfPolygon(parcelId);
            if (polyline == null)
                return null;

            var result = polyline.GetOffsetCurves(offset)[0] as Polyline;
            if (result.Area.Smaller(polyline.Area))
            {
                result.Dispose();
                result = polyline.GetOffsetCurves(-offset)[0] as Polyline;
            }

            polyline.Dispose();
            return result;
        }

        public static Polyline GetOffsetOfPolygon(ObjectId polygonId, double offset)
        {
            Polyline result = null;
            var database = polygonId.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var curve = transaction.GetObject(polygonId, OpenMode.ForRead) as Curve;
                if (curve != null)
                {
                    var curves = curve.GetOffsetCurves(offset);
                    if (curves.Count > 0)
                    {
                        var vertices = GetDistinctVertices2D(curves[0] as Curve, transaction);
                        if (vertices.Count > 0)
                        {
                            if(vertices[0] != vertices[vertices.Count - 1])
                                vertices.Add(vertices[0]);

                            result = new Polyline();
                            int i = 0;
                            foreach (var point2D in vertices)
                            {
                                result.AddVertexAt(i, point2D, 0, 0, 0);
                                i++;
                            }
                        }
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        /// <summary>
        /// Trim curves by a polygon.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="drawingDb"></param>
        /// <param name="keepInside"></param>
        /// <param name="spaceId"></param>
        /// <returns>
        /// Each pair of returned dictionary corresponds such situation:
        /// 1. If pair's value is empty array, means this curve is erased.
        /// 2. If pair's value is equal to pair's key, means this curve is remained.
        /// 3. else means this curve is trimed.
        /// </returns>
        public static Dictionary<ObjectId, ObjectId[]> TrimCurvesByPolygon(Polyline polygon, Database drawingDb, bool keepInside,
            bool mergeConnected = false, ObjectId? spaceId = null)
        {
            using (var transaction = drawingDb.TransactionManager.StartTransaction())
            {
                var modelspaceId = spaceId != null ? spaceId.Value : SymbolUtilityServices.GetBlockModelSpaceId(drawingDb);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                var ids = new List<ObjectId>();
                foreach (ObjectId objectId in modelspace)
                {
                    ids.Add(objectId);
                }
                var result = TrimCurvesByPolygon(polygon, ids, keepInside, modelspace, mergeConnected, transaction);
                transaction.Commit();
                return result;
            }
        }

        /// <summary>
        /// Trim curves by a polygon, the trimed part will be removed.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="curveIds"></param>
        /// <param name="keepInside"></param>
        /// <param name="space"></param>
        /// <param name="mergeConnected">
        /// A curve will be split into serval parts when trimming, if this value is true and
        /// there are some parts are connected, they will be merged into one.
        /// </param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static Dictionary<ObjectId, ObjectId[]> TrimCurvesByPolygon(Polyline polygon,
            IEnumerable<ObjectId> curveIds, bool keepInside, BlockTableRecord space, 
            bool mergeConnected, Transaction transaction)
        {
            var result = new Dictionary<ObjectId, ObjectId[]>();
            using (var tolerance = new SafeToleranceOverride())
            {
                foreach (var objectId in curveIds)
                {
                    var newIds = CurveUtils.TrimCurveByPolygon(objectId, polygon, space, keepInside, mergeConnected, transaction);
                    result.Add(objectId, newIds.ToArray());
                }
            }
            return result;
        }

        public static Dictionary<ObjectId, ObjectId[]> TrimCurvesByExtents(Extents3d extents, Database drawingDb, bool keepInside,
            bool mergeConnected = false, ObjectId? spaceId = null)
        {
            var result = new Dictionary<ObjectId, ObjectId[]>();
            var polyline = new Polyline();
            polyline.AddVertexAt(0, new Point2d(extents.MinPoint.X, extents.MinPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(extents.MinPoint.X, extents.MaxPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(extents.MaxPoint.X, extents.MinPoint.Y), 0, 0, 0);
            polyline.Closed = true;

            using (var transaction = drawingDb.TransactionManager.StartTransaction())
            {
                var modelspaceId = spaceId != null ? spaceId.Value : SymbolUtilityServices.GetBlockModelSpaceId(drawingDb);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                var ids = new List<ObjectId>();
                foreach (ObjectId objectId in modelspace)
                {
                    ids.Add(objectId);
                }
                using (var tolerance = new SafeToleranceOverride())
                {
                    foreach (var objectId in ids)
                    {
                        try
                        {
                            // 裁剪构造线会失败，所以加try/catch
                            var newIds = CurveUtils.TrimCurveByPolygon(objectId, polyline, modelspace, keepInside, mergeConnected, transaction);
                            result.Add(objectId, newIds.ToArray());
                        }
                        catch
                        {
                            
                        }
                    }
                }
                transaction.Commit();
            }
            polyline.Dispose();
            return result;
        }

        /// <summary>
        /// Trim curves by a polygon, the part out of the polygon will be removed.
        /// </summary>
        /// <param name="curveId"></param>
        /// <param name="polygon"></param>
        /// <param name="space"></param>
        /// <param name="keepInside"></param>
        /// <param name="transaction"></param>
        /// <returns>return the newly created entities.</returns>
        public static IEnumerable<ObjectId> TrimCurveByPolygon(ObjectId curveId, Polyline polygon, BlockTableRecord space,
            bool keepInside, bool mergeConnected, Transaction transaction)
        {
            var result = new List<ObjectId>();
            var curve = transaction.GetObject(curveId, OpenMode.ForRead) as Curve;
            if (curve == null)
                return result;

            var vertices = GetDistinctVertices2D(polygon);
            if(vertices[0] != vertices[vertices.Count-1])
                vertices.Add(vertices[0]);
            var vertexArray = vertices.ToArray();

            Point3dCollection points = new Point3dCollection();
            polygon.IntersectWith(curve, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count <= 0) // No intersection
            {
                // Select any point of curve to check whether it's in or out of polygon
                var anyPoint = new Point2d(curve.StartPoint.X, curve.EndPoint.Y);
                bool isIn = ComputerGraphics.IsInPolygon(vertexArray, anyPoint, vertices.Count - 1);
                if ((!isIn && keepInside) || (isIn && !keepInside))
                {
                    // If it's out of polygon, just erase.
                    curve.UpgradeOpen();
                    curve.Erase();
                }
                else
                {
                    // The curve is remained, store it in result.
                    result.Add(curveId);
                }
                return result;
            }

            // Intersection.
            var pointList = new List<Point3d>();
            foreach (Point3d point3D in points)
            {
                pointList.Add(point3D);
            }

            DBObjectCollection spliltCurves = CurveUtils.SplitCurve(curve, pointList.ToArray());

            var keeps = new List<Curve>();
            var disposeCurves = new List<Curve>();
            foreach (Curve splitCurve in spliltCurves)
            {
                // Check whether this curve is out of the polygon.
                bool isIn = true;
                try
                {
                    // Use a rather fast method to check whether mid point is in polygon.
                    // But splitCurve.GetPointAtParameter will throw exception occasionally.
                    var midParam = (splitCurve.StartParam + splitCurve.EndParam) / 2.0;
                    var midPoint = splitCurve.GetPointAtParameter(midParam);

                    var midPoint2d = new Point2d(midPoint.X, midPoint.Y);
                    isIn = ComputerGraphics.IsInPolygon(vertexArray, midPoint2d, vertexArray.Length - 1);
                }
                catch (Exception)
                {
                    // If exception is thrown, means curve's length is zero, just dispose it.
                    isIn = !keepInside;
                }

                if ((!isIn && keepInside) || (isIn && !keepInside))
                {
                    disposeCurves.Add(splitCurve);
                }
                else
                {
                    keeps.Add(splitCurve);
                }
            }

            // Merge the kept curves.
            if (mergeConnected)
            {
                List<Curve> immergables = null;
                var polylines = MergeCurves(keeps, transaction, out immergables);
                keeps.Clear();
                keeps.AddRange(polylines);
                keeps.AddRange(immergables);
            }

            // Add the kept curves into database.
            foreach (var keep in keeps)
            {
                var objId = space.AppendEntity(keep);
                transaction.AddNewlyCreatedDBObject(keep, true);
                result.Add(objId);
            }

            if (spliltCurves.Count > 0)
            {
                curve.UpgradeOpen();
                curve.Erase();
            }

            foreach (Curve disposeCurve in disposeCurves)
            {
                disposeCurve.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Calculate the minimum area surrounding box.
        /// http://gis.stackexchange.com/questions/22895/how-to-find-the-minimum-area-rectangle-for-given-points/22904#22904
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Point3d[] CalcMinimumSurroundingBox(IEnumerable<Point3d> points)
        {
            if(points == null || !points.Any())
                return new Point3d[0];

            // Calculate convex hull of points.
            var convex = new ConvexHull<Point3d>(points, it => it.X, it => it.Y,
                    (x, y) => new Point3d(x, y, 0), (a, b) => a == b);
            convex.CalcConvexHull();

            var hullPoints = convex.GetResultsAsArrayOfPoint();
            // There are duplicate points in hullPoints.
            hullPoints = GetDistinctVertices(hullPoints).ToArray();

            //  http://sourceforge.net/p/opencarto/code/HEAD/tree/trunk/server/src/main/java/org/opencarto/algo/base/SmallestSurroundingRectangle.java
            double minArea = double.MaxValue;
            Point3d[] rect = null;
            for (int i = 0; i < hullPoints.Length - 1; i++)
            {
                // Rotate hull points.
                var start = hullPoints[i];
                var end = hullPoints[i + 1];
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var matrix = Matrix3d.Rotation(-angle, Vector3d.ZAxis, start);
                var tempPoints = new List<Point3d>();
                foreach (var hullPoint in hullPoints)
                {
                    var tempPoint = hullPoint.TransformBy(matrix);
                    tempPoints.Add(tempPoint);
                }

                // Calculate bounding box and minimum area.
                var extents = CalcMinimumBoundingBox(tempPoints);
                var area = (extents.MaxPoint.X - extents.MinPoint.X) * (extents.MaxPoint.Y - extents.MinPoint.Y);
                if (area.Smaller(minArea))
                {
                    var inverse = matrix.Inverse();
                    minArea = area;

                    var minPoint = extents.MinPoint;
                    var maxPoint = extents.MaxPoint;
                    rect = new Point3d[]
                    {
                        minPoint.TransformBy(inverse),
                        new Point3d(minPoint.X, maxPoint.Y, 0.0).TransformBy(inverse),
                        maxPoint.TransformBy(inverse),
                        new Point3d(maxPoint.X, minPoint.Y, 0.0).TransformBy(inverse) 
                    };
                }
            }

            return rect;
        }

        private static Extents3d CalcMinimumBoundingBox(IEnumerable<Point3d> points)
        {
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;

            foreach (var point3D in points)
            {
                if (point3D.X.Smaller(xMin))
                    xMin = point3D.X;
                if (point3D.X.Larger(xMax))
                    xMax = point3D.X;

                if (point3D.Y.Smaller(yMin))
                    yMin = point3D.Y;
                if (point3D.Y.Larger(yMax))
                    yMax = point3D.Y;
            }

            return new Extents3d(new Point3d(xMin, yMin, 0.0), new Point3d(xMax, yMax, 0.0));
        }

        /// <summary>
        /// Get distincet vertices of polyline.
        /// Sometimes a polyline has duplicate vertices.
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns></returns>
        private static List<Point3d> GetDistinctVertices(Polyline polyline)
        {
            var distinctVertices = new List<Point3d>();

            var prevVertex = polyline.GetPoint3dAt(0);
            distinctVertices.Add(prevVertex);

            for (int i = 1; i < polyline.NumberOfVertices; i++)
            {
                var currentVertex = polyline.GetPoint3dAt(i);
                if (currentVertex != prevVertex)
                {
                    distinctVertices.Add(currentVertex);
                    prevVertex = currentVertex;
                }
            }

            return distinctVertices;
        }

        private static List<Point3d> GetDistinctVertices(Line line)
        {
            var result = new List<Point3d>();
            result.Add(line.StartPoint);
            if (!line.EndPoint.IsEqualTo(line.StartPoint))
                result.Add(line.EndPoint);
            return result;
        }

        private static List<Point2d> GetDistinctVertices2D(Line line)
        {
            var distinctVertices = GetDistinctVertices(line);
            return distinctVertices.Select(it => new Point2d(it.X, it.Y)).ToList();
        }

        /// <summary>
        /// Get distincet vertices of polyline.
        /// Sometimes a polyline has duplicate vertices.
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns></returns>
        private static List<Point2d> GetDistinctVertices2D(Polyline polyline)
        {
            var vertices = GetDistinctVertices(polyline);
            return vertices.Select(it => new Point2d(it.X, it.Y)).ToList();
        }

        private static List<Point3d> GetDistinctVertices(Polyline2d polyline2d, Transaction transaction)
        {
            var distinctVertices = new List<Point3d>();
            Point3d? prevPoint = null;
            foreach (var vertexId in polyline2d)
            {
                Vertex2d vertex = null;
                if (vertexId is ObjectId)
                {
                    var id = (ObjectId)vertexId;
                    if (id.IsValid)
                        vertex = transaction.GetObject((ObjectId)vertexId, OpenMode.ForRead) as Vertex2d;
                }
                else if (vertexId is Vertex2d)
                    vertex = (Vertex2d) vertexId;

                if (vertex == null)
                    continue;

                var point = vertex.Position;
                if (prevPoint == null || prevPoint.Value != point)
                {
                    distinctVertices.Add(point);
                    prevPoint = point;
                }
            }

            return distinctVertices;
        }

        private static List<Point2d> GetDistinctVertices2D(Polyline2d polyline2d, Transaction transaction)
        {
            var distinctVertices = GetDistinctVertices(polyline2d, transaction);
            return distinctVertices.Select(it => new Point2d(it.X, it.Y)).ToList();
        }

        private static IEnumerable<Point3d> GetDistinctVertices(IEnumerable<Point3d> points)
        {
            var result = new List<Point3d>();
            Point3d? prevPoint = null;
            foreach (var point in points)
            {
                if (prevPoint == null || prevPoint.Value != point)
                {
                    result.Add(point);
                    prevPoint = point;
                }
            }
            return result;
        }

        private static List<LineSegment2d> GenerateLineSegment2ds(List<Point2d> points)
        {
            var result = new List<LineSegment2d>();
            for (int i = 0; i < points.Count - 1; i ++)
            {
                var segment = new LineSegment2d(points[i], points[i + 1]);
                result.Add(segment);
            }
            return result;
        }

        private static List<LineSegment3d> GenerateLineSegment3ds(List<Point3d> points)
        {
            var result = new List<LineSegment3d>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var segment = new LineSegment3d(points[i], points[i + 1]);
                result.Add(segment);
            }
            return result;
        }

        public static IEnumerable<ObjectId> GetZeroAreaLoop(IEnumerable<ObjectId> allIds, Database database)
        {
            var results = new List<ObjectId>();
            using (var switcher = new SafeToleranceOverride())
            {
                using (var transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (var selectedObjectId in allIds)
                    {
                        var dbObj = transaction.GetObject(selectedObjectId, OpenMode.ForRead);
                        var curve = dbObj as Curve;
                        if (curve == null)
                            continue;

                        bool isClosed = false;
                        var polyline = curve as Polyline;
                        var polyline2d = curve as Polyline2d;
                        if (polyline != null)
                            isClosed = polyline.Closed;
                        else if (polyline2d != null)
                            isClosed = polyline2d.Closed;

                        if (!isClosed)
                        {
                            var startPoint = curve.StartPoint;
                            var endPoint = curve.EndPoint;
                            if (startPoint.IsEqualTo(endPoint))
                                isClosed = true;
                        }

                        if (isClosed && curve.Area.EqualsWithTolerance(0.0))
                        {
                            results.Add(selectedObjectId);
                        }
                    }
                    transaction.Commit();
                }
            }
            return results;
        }
    }
}
