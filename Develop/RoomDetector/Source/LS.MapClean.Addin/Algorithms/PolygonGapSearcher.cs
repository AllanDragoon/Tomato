using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public struct PolygonGap
    {
        /// <summary>
        /// Source polygon Id.
        /// </summary>
        public ObjectId SourceId { get; set; }
        /// <summary>
        /// Target polygon Id.
        /// </summary>
        public ObjectId TargetId { get; set; }

        /// <summary>
        /// Source segments which are near to target polygon.
        /// </summary>
        public KeyValuePair<Point2d, Point2d>[] SourceSegments { get; set; }

        /// <summary>
        /// Target segments which are near to source polygon.
        /// </summary>
        public KeyValuePair<Point2d, Point2d>[] TargetSegments { get; set; } 
    }

    public class CurveSegment : IDisposable
    {
        /// <summary>
        /// Geometry representation of CurveSegment.
        /// </summary>
        public LineSegment2d LineSegment { get; set; }

        /// <summary>
        /// Related curve id of AutoCAD.
        /// </summary>
        public ObjectId EntityId { get; set; }

        private bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            // If true, means called explicitly
            // If false, means called by Finiaizer.
            if (disposing)
            {
                // Free any other managed objects here.
                if (LineSegment != null)
                {
                    LineSegment.Dispose();
                    LineSegment = null;
                }
            }

            // Free any unmanaged objects here

            _disposed = true;
        }

        // They can provide a finalizer if needed. The finalizer must call Dispose(false).
        ~CurveSegment()
        {
            Dispose(false);
        }
    }

    public struct CurveSegmentVertex
    {
        public CurveSegmentVertex(CurveSegment curveSegment, bool start)
        {
            _segment = curveSegment;
            _start = start;
        }

        /// <summary>
        /// Whether it is start point
        /// </summary>
        private bool _start;

        /// <summary>
        /// Segment
        /// </summary>
        private CurveSegment _segment;
        public CurveSegment Segment
        {
            get { return _segment; }
        }

        /// <summary>
        /// Get SegmentVertex's point.
        /// </summary>
        public Point2d Point
        {
            get { return _start ? _segment.LineSegment.StartPoint : _segment.LineSegment.EndPoint; }
        }

        /// <summary>
        /// Get Segment's another point.
        /// </summary>
        public Point2d AnotherPoint
        {
            get { return _start ? _segment.LineSegment.EndPoint : _segment.LineSegment.StartPoint; }
        }
    }

//    public class PolygonGapSearcher : AlgorithmWithEditor
//    {
//        private double _tolerance = 0.5;

//        private IEnumerable<PolygonGap> _gaps;
//        public IEnumerable<PolygonGap> Gaps
//        {
//            get { return _gaps; }
//        }

//        public PolygonGapSearcher(Editor editor, double tolerance)
//            : base(editor)
//        {
//            _tolerance = tolerance;
//        }

//        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
//        {
//            if (!selectedObjectIds.Any())
//                return;

//            var watch = Stopwatch.StartNew();
//            var curveSegments = GetCurveSegments(selectedObjectIds);
//            var curveSegmentVertices = GetCurveSegmentVertices(curveSegments);
//            // Build a kd tree
//            var kdTree = new CurveVertexKdTree<CurveSegmentVertex>(curveSegmentVertices, it => it.Point, ignoreZ: true);
//            // Search near segment pairs
//            var nearSegmentPairs = new HashSet<KeyValuePair<CurveSegment, CurveSegment>>();
//            var invalidPairs = new HashSet<KeyValuePair<CurveSegment, CurveSegment>>();
//            using (var transaction = Editor.Document.Database.TransactionManager.StartTransaction())
//            {
//                foreach (var curveSegment in curveSegments)
//                {
//                    var nearSegments = GetNearCurveSegments(curveSegment, kdTree, transaction, nearSegmentPairs, invalidPairs);
//                    foreach (var nearSegment in nearSegments)
//                    {
//                        nearSegmentPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(curveSegment, nearSegment));
//                    }
//                }
//            }

//            // Post process for nearSegmentPairs
//            _gaps = GetPolygonGapsFromSegmentPair(nearSegmentPairs);

//            foreach (var curveSegment in curveSegments)
//            {
//                curveSegment.Dispose();
//            }
//            watch.Stop();
//            var elapseMs = watch.ElapsedMilliseconds;
//#if DEBUG
//            System.Diagnostics.Debug.WriteLine("查找多边形间隙花费{0}毫秒", elapseMs);
//#endif
//        }

//        private bool IsCurveClosed(Curve curve)
//        {
//            bool closed = false;
//            var polyline = curve as Polyline;
//            var polyline2d = curve as Polyline2d;
//            if (polyline != null)
//                closed = polyline.Closed;
//            else if (polyline2d != null)
//                closed = polyline2d.Closed;
//            return closed;
//        }

//        private IEnumerable<CurveSegment> GetCurveSegments(IEnumerable<ObjectId> ids)
//        {
//            var result = new List<CurveSegment>();
//            var database = Editor.Document.Database;
//            using (var transaction = database.TransactionManager.StartTransaction())
//            {
//                foreach (var id in ids)
//                {
//                    var curve = transaction.GetObject(id, OpenMode.ForRead) as Curve;
//                    if (curve == null)
//                        continue;
//                    if (!IsCurveClosed(curve))
//                        continue;

//                    var segments = CurveUtils.GetSegment3dsOfCurve(curve, transaction);
//                    foreach (var segment in segments)
//                    {
//                        result.Add(new CurveSegment()
//                        {
//                            EntityId = id, 
//                            LineSegment = segment
//                        });
//                    }
//                }
//                transaction.Commit();
//            }
//            return result;
//        }

//        private IEnumerable<CurveSegmentVertex> GetCurveSegmentVertices(IEnumerable<CurveSegment> segments)
//        {
//            var result = new List<CurveSegmentVertex>();
//            foreach (var curveSegment in segments)
//            {
//                result.Add(new CurveSegmentVertex(curveSegment, true));
//                result.Add(new CurveSegmentVertex(curveSegment, false));
//            }
//            return result;
//        }

//        private IEnumerable<CurveSegment> GetNearCurveSegments(CurveSegment sourceSegment,
//            CurveVertexKdTree<CurveSegmentVertex> kdtree, Transaction transaction, 
//            HashSet<KeyValuePair<CurveSegment, CurveSegment>> finalResult,
//            HashSet<KeyValuePair<CurveSegment, CurveSegment>> invalidPairs)
//        {
//            var result = new HashSet<CurveSegment>();
//            var extents = GetExtentsOfSegmentWithTolerance(sourceSegment.LineSegment, _tolerance);
//            var nearVertices = kdtree.BoxedRange(extents.MinPoint, extents.MaxPoint);

//            var line3d = new Line3d(sourceSegment.LineSegment.StartPoint, sourceSegment.LineSegment.EndPoint);
//            foreach (var nearVertex in nearVertices)
//            {
//                // If two segments' id are same, just continue.
//                if (nearVertex.Segment.EntityId == sourceSegment.EntityId)
//                    continue;
//                if (result.Contains(nearVertex.Segment))
//                    continue;

//                // If final result contains source segment and near segment, just continue.
//                if (finalResult.Contains(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment)) ||
//                    finalResult.Contains(new KeyValuePair<CurveSegment, CurveSegment>(nearVertex.Segment, sourceSegment)))
//                    continue;
                
//                // If they are coincide, just continue.
//                if (invalidPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment)) ||
//                    invalidPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(nearVertex.Segment, sourceSegment)))
//                    continue;

//                // Check distance between nearVertex and the line.
//                var dist1 = line3d.GetDistanceTo(nearVertex.Point);
//                if (dist1.Larger(_tolerance))
//                {
//                    invalidPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment));
//                    continue;
//                }

//                // Check distance between another vertex and the line
//                var dist2 = line3d.GetDistanceTo(nearVertex.AnotherPoint);
//                if (dist2.Larger(_tolerance))
//                {
//                    invalidPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment));
//                    continue;
//                }

//                // If they are both equal to 0.0, means duplicate, just return.
//                if (dist1.EqualsWithTolerance(0.0) && dist2.EqualsWithTolerance(0.0))
//                {
//                    invalidPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment));
//                    continue;
//                }

//                var lineSeg = sourceSegment.LineSegment;
//                var targetLineSeg = nearVertex.Segment.LineSegment;

//                // source segment and target segment's angle should less than 20
//                var sourceVector = lineSeg.Direction;
//                var targetVector = targetLineSeg.Direction;
//                var angle = sourceVector.GetAngleTo(targetVector);
//                if (angle.Larger(Math.PI/9.0) && angle.Smaller(Math.PI*8.0/9.0))
//                {
//                    invalidPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(sourceSegment, nearVertex.Segment));
//                    continue;
//                }

//                var pt1OnSource = lineSeg.GetClosestPointTo(nearVertex.Point);
//                var pt2OnSource = lineSeg.GetClosestPointTo(nearVertex.AnotherPoint);
//                var ptStartOnTarget = targetLineSeg.GetClosestPointTo(lineSeg.StartPoint);
//                var ptEndOnTarget = targetLineSeg.GetClosestPointTo(lineSeg.EndPoint);
//                if ((pt1OnSource.Point.Equals(lineSeg.StartPoint) || pt1OnSource.Point.Equals(lineSeg.EndPoint)) &&
//                    (pt2OnSource.Point.Equals(lineSeg.StartPoint) || pt2OnSource.Point.Equals(lineSeg.EndPoint)) &&
//                    (ptStartOnTarget.Point.Equals(targetLineSeg.StartPoint)|| ptStartOnTarget.Point.Equals(targetLineSeg.EndPoint)) &&
//                    (ptEndOnTarget.Point.Equals(targetLineSeg.StartPoint) || ptEndOnTarget.Point.Equals(targetLineSeg.EndPoint)))
//                    continue;

//                // Check whether these two are on source curve.
//                var curve = (Curve)transaction.GetObject(sourceSegment.EntityId, OpenMode.ForRead);
//                if (GeometryUtils.IsPointOnCurveGCP(curve, nearVertex.Point) &&
//                    GeometryUtils.IsPointOnCurveGCP(curve, nearVertex.AnotherPoint))
//                    continue;

//                result.Add(nearVertex.Segment);
//            }
//            line3d.Dispose();
//            return result;
//        }

//        private static Extents3d GetExtentsOfSegmentWithTolerance(LineSegment3d segment, double tolerance)
//        {
//            var vector = segment.Direction.GetNormal();
//            var perpVector = vector.CrossProduct(Vector3d.ZAxis).GetNormal() * tolerance;
//            var point1 = segment.StartPoint + perpVector;
//            var point2 = segment.StartPoint - perpVector;
//            var point3 = segment.EndPoint + perpVector;
//            var point4 = segment.EndPoint - perpVector;

//            var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
//            var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
//            var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
//            var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

//            return new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
//        }

//        private IEnumerable<PolygonGap> GetPolygonGapsFromSegmentPair(
//            IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> pairs)
//        {
//            var result = new List<PolygonGap>();
//            var groupsBySourceId = pairs.GroupBy(it => it.Key.EntityId);
//            foreach (var group in groupsBySourceId)
//            {
//                var sourceId = group.Key;

//                // sourceAsTargets's key is target curve.
//                var sourceAsTargets = pairs.Where(it => it.Value.EntityId == sourceId)
//                    .GroupBy(it => it.Key.EntityId);

//                var groupsByTargetId = group.GroupBy(it => it.Value.EntityId);
//                foreach (var subgroup in groupsByTargetId)
//                {
//                    // subgroup's key is target curve.
//                    var targetId = subgroup.Key;
//                    // Check whethe it has been handled.
//                    var gap = result.FirstOrDefault(it => it.SourceId == targetId);
//                    if (!gap.Equals(default(PolygonGap)))
//                        continue;

//                    var sourceSegments = new List<CurveSegment>();
//                    var targetSegments = new List<CurveSegment>();
//                    foreach (var p in subgroup)
//                    {
//                        sourceSegments.Add(p.Key);
//                        targetSegments.Add(p.Value);
//                    }

//                    var sourceAsTarget = sourceAsTargets.FirstOrDefault(it => it.Key == targetId);
//                    if (sourceAsTarget != null)
//                    {
//                        foreach (var p in sourceAsTarget)
//                        {
//                            sourceSegments.Add(p.Value);
//                            targetSegments.Add(p.Key);
//                        }
//                    }

//                    var sourcePointPairs = sourceSegments.Select(
//                        it => new KeyValuePair<Point3d, Point3d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
//                        .ToArray();
//                    var targetPointParis = targetSegments.Select(
//                        it => new KeyValuePair<Point3d, Point3d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
//                        .ToArray();
//                    result.Add(new PolygonGap()
//                    {
//                        SourceId = sourceId,
//                        TargetId = targetId,
//                        //SourceSegments = sourcePointPairs,
//                        //TargetSegments = targetPointParis
//                    });

//                }
//            }
//            return result;
//        }
//    }

    class BspSegmentForCollsion : BspSegment
    {
        public BspSegment SourceSegment { get; set; }

        public static IEnumerable<BspSegmentForCollsion> CreateSegmentsForCollision(BspSegment segment, double range)
        {
            var startPoint = segment.LineSegment.StartPoint;
            var endPoint = segment.LineSegment.EndPoint;
            var direction = segment.LineSegment.Direction;
            var perp = direction.GetPerpendicularVector().GetNormal() * range;
            var topleft = startPoint + perp;
            var topright = endPoint + perp;
            var bottomleft = startPoint - perp;
            var bottomright = endPoint - perp;
            var topSegment = new BspSegmentForCollsion()
            {
                EntityId = segment.EntityId,
                LineSegment = new LineSegment2d(topleft, topright),
                SourceSegment = segment
            };
            var rightSegment = new BspSegmentForCollsion()
            {
                EntityId = segment.EntityId,
                LineSegment = new LineSegment2d(topright, bottomright),
                SourceSegment = segment
            };
            var bottomSegment = new BspSegmentForCollsion()
            {
                EntityId = segment.EntityId,
                LineSegment = new LineSegment2d(bottomright, bottomleft),
                SourceSegment = segment
            };
            var leftSegment = new BspSegmentForCollsion()
            {
                EntityId = segment.EntityId,
                LineSegment = new LineSegment2d(bottomleft, topleft),
                SourceSegment = segment
            };
            return new BspSegmentForCollsion[]
            {
                topSegment,
                rightSegment,
                bottomSegment,
                leftSegment
            };
        }
    }
    /// <summary>
    /// Use 2D collision detection methods to search gaps.
    /// </summary>
    public class PolygonGapSearcherBspTree : AlgorithmWithEditor
    {
        private double _tolerance = 0.5;

        private IEnumerable<PolygonGap> _gaps;
        public IEnumerable<PolygonGap> Gaps
        {
            get { return _gaps; }
        }

        public PolygonGapSearcherBspTree(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var originBspSegments = GetAllBspSegments(selectedObjectIds);
            var bspSegmentForCollisions = new List<BspSegmentForCollsion>();
            foreach (var bspSegment in originBspSegments)
            {
                // 将每个segment向外扩，形成一个矩形，矩形的宽为tolerance，但为了保险再向外扩0.001
                var bspSegments = BspSegmentForCollsion.CreateSegmentsForCollision(bspSegment, _tolerance / 2.0 + 0.001);
                bspSegmentForCollisions.AddRange(bspSegments);
            }

            var bsptree = new Curve2dBspTree(bspSegmentForCollisions);
            var nearSegments = SearchNearSegments(bsptree);
            var qualified = FilterNearSegments(nearSegments);

            // Post process for nearSegmentPairs
            _gaps = GetPolygonGapsFromSegmentPair(qualified);

            foreach (var curveSegment in originBspSegments)
            {
                curveSegment.Dispose();
            }
            foreach (var bspSegmentForCollision in bspSegmentForCollisions)
            {
                bspSegmentForCollision.Dispose();
            }
        }

        IEnumerable<KeyValuePair<BspSegment, BspSegment>> FilterNearSegments(
            IEnumerable<KeyValuePair<BspSegment, BspSegment>> nearSegments)
        {
            var result = new List<KeyValuePair<BspSegment, BspSegment>>();
            foreach (var keyValuePair in nearSegments)
            {
                if (!IsSegmentsReallyNear(keyValuePair.Key.LineSegment, keyValuePair.Value.LineSegment))
                    continue;
                result.Add(keyValuePair);
            }
            return result;
        }

        bool IsSegmentsReallyNear(LineSegment2d source, LineSegment2d target)
        {
            // Duplicate should return.
            if (source.StartPoint == target.StartPoint && source.EndPoint == target.EndPoint ||
                source.EndPoint == target.StartPoint && source.StartPoint == target.EndPoint)
                return false;

            if (source.IsColinearTo(target))
                return false;

            // source segment and target segment's angle should less than 20
            var sourceVector = source.Direction;
            var targetVector = target.Direction;
            var angle = sourceVector.GetAngleTo(targetVector);
            if (angle.Larger(Math.PI / 36.0) && angle.Smaller(Math.PI * 35.0 / 36.0))
                return false;

            // Check two segments' distance
            var sourceLine2d = new Line2d(source.StartPoint, source.EndPoint);
            var targetLine2d = new Line2d(target.StartPoint, target.EndPoint);
            var dist1 = sourceLine2d.GetDistanceTo(target.StartPoint);
            var dist2 = sourceLine2d.GetDistanceTo(target.EndPoint);
            // If they are both equal to 0.0, means duplicate, just return.
            if (dist1.EqualsWithTolerance(0.0, Tolerance.Global.EqualPoint) && dist2.EqualsWithTolerance(0.0, Tolerance.Global.EqualPoint))
                return false;

            var dist3 = targetLine2d.GetDistanceTo(source.StartPoint);
            var dist4 = targetLine2d.GetDistanceTo(source.EndPoint);
            if ((dist1.Larger(_tolerance) || dist2.Larger(_tolerance)) && 
                (dist3.Larger(_tolerance) || dist4.Larger(_tolerance)))
                return false;


            // Filter out the situation if a short segment (length < _tolerance) is connect to another.
            var pt1OnSource = source.GetClosestPointTo(target.StartPoint);
            var pt2OnSource = source.GetClosestPointTo(target.EndPoint);
            
            if (pt1OnSource.Point == pt2OnSource.Point)
                return false;
            
            return true;
        }

        IEnumerable<KeyValuePair<BspSegment, BspSegment>> SearchNearSegments(Curve2dBspTree bsptree)
        {
            var result = new HashSet<KeyValuePair<BspSegment, BspSegment>>();
            SearchNearSegmentsSegmentsOfNode(bsptree.Root, result);
            return result;
        }

        void SearchNearSegmentsSegmentsOfNode(CurveBspNode node, HashSet<KeyValuePair<BspSegment, BspSegment>> result)
        {
            var segments = node.Segments;
            foreach (var bspSegment in segments)
            {
                var originalestTarget = GetOriginalSourceBspSegment(bspSegment);
                if (bspSegment.StartSplitInfos != null)
                {
                    foreach (var splitInfo in bspSegment.StartSplitInfos)
                    {
                        // Self intersect is ignored.
                        if (splitInfo.SourceSegment.EntityId == bspSegment.EntityId)
                            continue;

                        // If not real intersection.
                        var extendType = CurveIntersectUtils.ParamToExtendTypeForLine(splitInfo.SourceParam);
                        if (extendType != ExtendType.None)
                            continue;

                        var originalestSource = GetOriginalSourceBspSegment(splitInfo.SourceSegment);
                        if (result.Contains(new KeyValuePair<BspSegment, BspSegment>(originalestSource, originalestTarget)) ||
                            result.Contains(new KeyValuePair<BspSegment, BspSegment>(originalestTarget, originalestSource)))
                            continue;

                        result.Add(new KeyValuePair<BspSegment, BspSegment>(originalestSource, originalestTarget));
                    }
                }
                if (bspSegment.EndSplitInfos != null)
                {
                    foreach (var splitInfo in bspSegment.EndSplitInfos)
                    {
                        // Self intersect is ignored.
                        if (splitInfo.SourceSegment.EntityId == bspSegment.EntityId)
                            continue;

                        // If not real intersection.
                        var extendType = CurveIntersectUtils.ParamToExtendTypeForLine(splitInfo.SourceParam);
                        if (extendType != ExtendType.None)
                            continue;

                        var originalestSource = GetOriginalSourceBspSegment(splitInfo.SourceSegment);
                        if (result.Contains(new KeyValuePair<BspSegment, BspSegment>(originalestSource, originalestTarget)) ||
                            result.Contains(new KeyValuePair<BspSegment, BspSegment>(originalestTarget, originalestSource)))
                            continue;

                        result.Add(new KeyValuePair<BspSegment, BspSegment>(originalestSource, originalestTarget));
                    }
                }
            }

            if (node.LeftChild != null)
                SearchNearSegmentsSegmentsOfNode(node.LeftChild, result);

            if (node.RightChild != null)
                SearchNearSegmentsSegmentsOfNode(node.RightChild, result);
        }

        BspSegment GetOriginalSourceBspSegment(BspSegment segment)
        {
            var bspSegment = segment;
            while (bspSegment.OriginalSegment != null)
            {
                bspSegment = bspSegment.OriginalSegment;
            }
            var collisionBspSegment = bspSegment as BspSegmentForCollsion;
            if(collisionBspSegment == null)
                throw new InvalidProgramException("不是BspSegmentForCollsion！");
            return collisionBspSegment.SourceSegment;
        }

        List<BspSegment> GetAllBspSegments(IEnumerable<ObjectId> selectedObjectIds)
        {
            var originSegments = new List<BspSegment>();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    var segments = CurveUtils.GetSegment2dsOfCurve(curve, transaction);
                    originSegments.AddRange(segments.Select(it => new BspSegment()
                    {
                        LineSegment = it,
                        EntityId = objId
                    }));
                }
                transaction.Commit();
            }
            return originSegments;
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

        private IEnumerable<PolygonGap> GetPolygonGapsFromSegmentPair(
            IEnumerable<KeyValuePair<BspSegment, BspSegment>> pairs)
        {
            var result = new List<PolygonGap>();
            var groupsBySourceId = pairs.GroupBy(it => it.Key.EntityId);
            foreach (var group in groupsBySourceId)
            {
                var sourceId = group.Key;

                // sourceAsTargets's key is target curve.
                var sourceAsTargets = pairs.Where(it => it.Value.EntityId == sourceId)
                    .GroupBy(it => it.Key.EntityId);

                var groupsByTargetId = group.GroupBy(it => it.Value.EntityId);
                foreach (var subgroup in groupsByTargetId)
                {
                    // subgroup's key is target curve.
                    var targetId = subgroup.Key;
                    // Check whethe it has been handled.
                    var gap = result.FirstOrDefault(it => it.SourceId == targetId);
                    if (!gap.Equals(default(PolygonGap)))
                        continue;

                    var sourceSegments = new List<BspSegment>();
                    var targetSegments = new List<BspSegment>();
                    foreach (var p in subgroup)
                    {
                        sourceSegments.Add(p.Key);
                        targetSegments.Add(p.Value);
                    }

                    var sourceAsTarget = sourceAsTargets.FirstOrDefault(it => it.Key == targetId);
                    if (sourceAsTarget != null)
                    {
                        foreach (var p in sourceAsTarget)
                        {
                            sourceSegments.Add(p.Value);
                            targetSegments.Add(p.Key);
                        }
                    }

                    var sourcePointPairs = sourceSegments.Select(
                        it => new KeyValuePair<Point2d, Point2d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
                        .ToArray();
                    var targetPointParis = targetSegments.Select(
                        it => new KeyValuePair<Point2d, Point2d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
                        .ToArray();
                    result.Add(new PolygonGap()
                    {
                        SourceId = sourceId,
                        TargetId = targetId,
                        SourceSegments = sourcePointPairs,
                        TargetSegments = targetPointParis
                    });

                }
            }
            return result;
        }
    }

    /// <summary>
    /// Use Kdtree and 2d collision to search polygon gap.
    /// </summary>
    public class PolygonGapSearcherKdTree : AlgorithmWithEditor
    {
         private double _tolerance = 0.5;

        private IEnumerable<PolygonGap> _gaps;
        public IEnumerable<PolygonGap> Gaps
        {
            get { return _gaps; }
        }

        public PolygonGapSearcherKdTree(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return;

            var segmentPairs = SearchNearSegments(selectedObjectIds, _tolerance);

            var qualified = FilterNearSegments(segmentPairs);
            // Post process for nearSegmentPairs
            _gaps = GetPolygonGapsFromSegmentPair(qualified);
        }

        public static IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> SearchNearSegments(IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var segments = GetAllCurveSegments(selectedObjectIds, tolerance, onlyForClosedPolygon:true);
            var vertices = new List<CollisionVertex>();
            foreach (var segment in segments)
            {
                var collisionVertices = segment.MiniBoundingBox.Select(it => new CollisionVertex()
                {
                    Point = new Point3d(it.X, it.Y, 0.0),
                    Segment = segment
                });
                vertices.AddRange(collisionVertices);
            }

            // Create kd tree
            var kdTree = new CurveVertexKdTree<CollisionVertex>(vertices, it => it.Point.ToArray(), ignoreZ: true);
            // Use kd tree to check collision bounding box's intersection
            var segmentPairs = new HashSet<KeyValuePair<CurveSegment, CurveSegment>>();
            foreach (var segment in segments)
            {
                var extents = segment.GetExtents();
                if (extents == null)
                    continue;
                var minPoint = extents.Value.MinPoint;
                var maxPoint = extents.Value.MaxPoint;
                var nearVertices = kdTree.BoxedRange(new double[] { minPoint.X, minPoint.Y, 0.0 },
                    new double[] { maxPoint.X, maxPoint.Y, 0.0 });
                foreach (var collisionVertex in nearVertices)
                {
                    if (collisionVertex.Segment.EntityId == segment.EntityId ||
                        segmentPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(segment, collisionVertex.Segment)) ||
                        segmentPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(collisionVertex.Segment, segment)))
                        continue;

                    var boundingBox = segment.MiniBoundingBox.ToList();
                    boundingBox.Add(segment.MiniBoundingBox[0]);
                    if (!ComputerGraphics.IsInPolygon(boundingBox.ToArray(), new Point2d(collisionVertex.Point.X, collisionVertex.Point.Y), 4))
                        continue;

                    segmentPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(segment, collisionVertex.Segment));
                }
            }

            return segmentPairs;
        }

        public static List<CurveSegmentForCollision> GetAllCurveSegments(IEnumerable<ObjectId> selectedObjectIds, double tolerance, bool onlyForClosedPolygon)
        {
            var originSegments = new List<CurveSegmentForCollision>();
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return originSegments;

            var database = selectedObjectIds.First().Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (onlyForClosedPolygon && !IsCurveClosed(curve))
                        continue;

                    var segments = CurveUtils.GetSegment2dsOfCurve(curve, transaction);
                    originSegments.AddRange(segments.Select(it => new CurveSegmentForCollision()
                    {
                        LineSegment = it,
                        EntityId = objId,
                        MiniBoundingBox = CurveSegmentForCollision.CreateCollisionBoundingBox(it, tolerance / 2.0 + 0.001)
                    }));
                }
                transaction.Commit();
            }
            return originSegments;
        }

        private static bool IsCurveClosed(Curve curve)
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

        IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> FilterNearSegments(
            IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> nearSegments)
        {
            var result = new List<KeyValuePair<CurveSegment, CurveSegment>>();
            foreach (var keyValuePair in nearSegments)
            {
                if (!IsSegmentsReallyNear(keyValuePair.Key.LineSegment, keyValuePair.Value.LineSegment))
                    continue;
                result.Add(keyValuePair);
            }
            return result;
        }

        bool IsSegmentsReallyNear(LineSegment2d source, LineSegment2d target)
        {
            // Duplicate should return.
            if (source.StartPoint == target.StartPoint && source.EndPoint == target.EndPoint ||
                source.EndPoint == target.StartPoint && source.StartPoint == target.EndPoint)
                return false;

            if (IsColinear(source, target))
                return false;

            // source segment and target segment's angle should less than 20
            var sourceVector = source.Direction;
            var targetVector = target.Direction;
            var angle = sourceVector.GetAngleTo(targetVector);
            if (angle.Larger(Math.PI / 36.0) && angle.Smaller(Math.PI * 35.0 / 36.0))
                return false;

            // Check two segments' distance
            var sourceLine2d = new Line2d(source.StartPoint, source.EndPoint);
            var targetLine2d = new Line2d(target.StartPoint, target.EndPoint);
            var dist1 = sourceLine2d.GetDistanceTo(target.StartPoint);
            var dist2 = sourceLine2d.GetDistanceTo(target.EndPoint);
            // If they are both equal to 0.0, means duplicate, just return.
            if (dist1.EqualsWithTolerance(0.0, Tolerance.Global.EqualPoint) && dist2.EqualsWithTolerance(0.0, Tolerance.Global.EqualPoint))
                return false;

            var dist3 = targetLine2d.GetDistanceTo(source.StartPoint);
            var dist4 = targetLine2d.GetDistanceTo(source.EndPoint);
            if ((dist1.Larger(_tolerance) || dist2.Larger(_tolerance)) &&
                (dist3.Larger(_tolerance) || dist4.Larger(_tolerance)))
                return false;


            // Filter out the situation if a short segment (length < _tolerance) is connect to another.
            var pt1OnSource = source.GetClosestPointTo(target.StartPoint);
            var pt2OnSource = source.GetClosestPointTo(target.EndPoint);

            if (pt1OnSource.Point == pt2OnSource.Point)
                return false;

            return true;
        }

        private bool IsColinear(LineSegment2d source, LineSegment2d target)
        {
            var length = source.Length;
            if (length.EqualsWithTolerance(0.0))
                return false;

            var leftStart = ComputerGraphics.IsLeft(source.StartPoint, source.EndPoint, target.StartPoint) / length;
            var leftEnd = ComputerGraphics.IsLeft(source.StartPoint, source.EndPoint, target.EndPoint) / length;
            if (leftStart.EqualsWithTolerance(0.0) && leftEnd.EqualsWithTolerance(0.0))
                return true;
            return false;
        }

        private IEnumerable<PolygonGap> GetPolygonGapsFromSegmentPair(
            IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> pairs)
        {
            var result = new List<PolygonGap>();
            var groupsBySourceId = pairs.GroupBy(it => it.Key.EntityId);
            foreach (var group in groupsBySourceId)
            {
                var sourceId = group.Key;

                // sourceAsTargets's key is target curve.
                var sourceAsTargets = pairs.Where(it => it.Value.EntityId == sourceId)
                    .GroupBy(it => it.Key.EntityId);

                var groupsByTargetId = group.GroupBy(it => it.Value.EntityId);
                foreach (var subgroup in groupsByTargetId)
                {
                    // subgroup's key is target curve.
                    var targetId = subgroup.Key;
                    // Check whethe it has been handled.
                    var gap = result.FirstOrDefault(it => it.SourceId == targetId);
                    if (!gap.Equals(default(PolygonGap)))
                        continue;

                    var sourceSegments = new List<CurveSegment>();
                    var targetSegments = new List<CurveSegment>();
                    foreach (var p in subgroup)
                    {
                        sourceSegments.Add(p.Key);
                        targetSegments.Add(p.Value);
                    }

                    var sourceAsTarget = sourceAsTargets.FirstOrDefault(it => it.Key == targetId);
                    if (sourceAsTarget != null)
                    {
                        foreach (var p in sourceAsTarget)
                        {
                            sourceSegments.Add(p.Value);
                            targetSegments.Add(p.Key);
                        }
                    }

                    var sourcePointPairs = sourceSegments.Select(
                        it => new KeyValuePair<Point2d, Point2d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
                        .ToArray();
                    var targetPointParis = targetSegments.Select(
                        it => new KeyValuePair<Point2d, Point2d>(it.LineSegment.StartPoint, it.LineSegment.EndPoint))
                        .ToArray();
                    result.Add(new PolygonGap()
                    {
                        SourceId = sourceId,
                        TargetId = targetId,
                        SourceSegments = sourcePointPairs,
                        TargetSegments = targetPointParis
                    });

                }
            }
            return result;
        }
    }

    public class CurveSegmentForCollision : CurveSegment
    {
        public Point2d[] MiniBoundingBox { get; set; }

        /// <summary>
        /// Get segment range box
        /// </summary>
        /// <returns></returns>
        public Extents2d? GetExtents()
        {
            if (MiniBoundingBox == null || !MiniBoundingBox.Any())
                return null;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (var point2D in MiniBoundingBox)
            {
                minX = Math.Min(minX, point2D.X);
                minY = Math.Min(minY, point2D.Y);
                maxX = Math.Max(maxX, point2D.X);
                maxY = Math.Max(maxY, point2D.Y);
            }
            return new Extents2d(new Point2d(minX, minY), new Point2d(maxX, maxY));
        }

        /// <summary>
        /// Create bounding box for collision.
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public static Point2d[] CreateCollisionBoundingBox(LineSegment2d segment, double range)
        {
            var startPoint = segment.StartPoint;
            var endPoint = segment.EndPoint;
            var direction = segment.Direction.GetNormal() * range;
            var perp = direction.GetPerpendicularVector().GetNormal() * range;
            var topleft = startPoint - direction + perp;
            var topright = endPoint + direction + perp;
            var bottomleft = startPoint - direction - perp;
            var bottomright = endPoint + direction - perp;

            return new Point2d[]
            {
                topleft,
                topright,
                bottomright,
                bottomleft
            };
        }
    }

    public class CollisionVertex
    {
        /// <summary>
        /// Vertex's point
        /// </summary>
        public Point3d Point { get; set; }

        /// <summary>
        /// Related collision segment
        /// </summary>
        public CurveSegmentForCollision Segment { get; set; }
    }
}
