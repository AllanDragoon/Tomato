using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GeoAPI.Geometries;
using LS.MapClean.Addin.QuadTree;
using LS.MapClean.Addin.Utils;
using NetTopologySuite.Index.Strtree;

namespace LS.MapClean.Addin.Algorithms
{
    public struct CurveCrossingPoints
    {
        public ObjectId CurveId;
        public Point3dCollection Points;
    }

//    public class BreakCrossingObjectsBsp : AlgorithmWithEditor
//    {
//        private IEnumerable<CurveCrossingInfo> _crossingInfos;
//        public IEnumerable<CurveCrossingInfo> CrossingInfos
//        {
//            get { return _crossingInfos; }
//        }

//        public BreakCrossingObjectsBsp(Editor editor)
//            : base(editor)
//        {
//        }

//        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
//        {
//#if DEBUG
//            var watch = Stopwatch.StartNew();
//#endif
//            if (selectedObjectIds == null || !selectedObjectIds.Any())
//                return;

//            // 调低计算精度，否则有些交叉因为精度问题算不出来
//            var oldTolerance = DoubleExtensions.STolerance;
//            DoubleExtensions.STolerance = 1e-04;
//            var database = Editor.Document.Database;
//            using (var transaction = database.TransactionManager.StartTransaction())
//            {
//                var curve2DBspBuilder = new Curve2dBspBuilder(selectedObjectIds, transaction);
//                IEnumerable<CurveCrossingInfo> duplicateEntities = null;
//                _crossingInfos = curve2DBspBuilder.SearchRealIntersections(true, out duplicateEntities);

//                transaction.Commit();
//            }
//            // 恢复默认的计算精度值
//            DoubleExtensions.STolerance = oldTolerance;

//            // the code that you want to measure comes here
//#if DEBUG
//            watch.Stop();
//            var elapsedMs = watch.ElapsedMilliseconds;
//            Editor.WriteMessage("\n查找交叉对象花费时间{0}毫秒", elapsedMs);
//#endif
//        }

//        public Dictionary<ObjectId, IEnumerable<ObjectId>> Fix(bool eraseOld)
//        {
//            var result = new Dictionary<ObjectId, IEnumerable<ObjectId>>();
//            if (_crossingInfos == null || !_crossingInfos.Any())
//                return result;

//            // Group intersect points by ObjectId.
//            Dictionary<ObjectId, List<Point3d>> intersects = null;
//            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
//            GroupIntersectPoints(out intersects, out selfIntersects);

//            var document = Editor.Document;
//            using (var docLock = document.LockDocument())
//            {
//                using (var transaction = document.Database.TransactionManager.StartTransaction())
//                {
//                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
//                    foreach (var pair in intersects)
//                    {
//                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, false, eraseOld);
//                        result.Add(pair.Key, ids);
//                    }

//                    foreach (var pair in selfIntersects)
//                    {
//                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, true, eraseOld);
//                        result.Add(pair.Key, ids);
//                    }

//                    transaction.Commit();
//                }
//            }
//            return result;
//        }

//        public IEnumerable<Entity> DummyFix()
//        {
//            if (_crossingInfos == null || !_crossingInfos.Any())
//                return new Entity[0];

//            // Group intersect points by ObjectId.
//            Dictionary<ObjectId, List<Point3d>> intersects = null;
//            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
//            GroupIntersectPoints(out intersects, out selfIntersects);

//            var result = new List<Entity>();
//            var document = Editor.Document;
//            using (var docLock = document.LockDocument())
//            {
//                using (var transaction = document.Database.TransactionManager.StartTransaction())
//                {
//                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
//                    foreach (var pair in intersects)
//                    {
//                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, false);
//                        foreach (Entity ent in ents)
//                        {
//                            result.Add(ent);
//                        }
//                    }

//                    foreach (var pair in selfIntersects)
//                    {
//                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, true);
//                        foreach (Entity ent in ents)
//                        {
//                            result.Add(ent);
//                        }
//                    }

//                    transaction.Commit();
//                }
//            }
//            return result;
//        }

//        private IEnumerable<ObjectId> SplitCurve(ObjectId objId, IEnumerable<Point3d> points, BlockTableRecord modelSpace,
//            Transaction transaction, bool selfIntersect, bool eraseOld)
//        {
//            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
//            if (curve == null)
//                return new ObjectId[0];

//            var result = new List<ObjectId>();
//            DBObjectCollection spliltCurves = null;
//            if (selfIntersect)
//            {
//                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
//            }
//            else
//            {
//                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
//            }

//            // The splitted curves has the same layer with original curve, 
//            // so we needn't set its layer explicitly.
//            foreach (Curve splitCurve in spliltCurves)
//            {
//                var id  = modelSpace.AppendEntity(splitCurve);
//                transaction.AddNewlyCreatedDBObject(splitCurve, true);
//                result.Add(id);
//            }

//            if (spliltCurves.Count > 0)
//            {
//                if (eraseOld)
//                {
//                    // Erase the old one
//                    curve.Erase();
//                }
//            }
//            else
//            {
//                result.Add(curve.Id);
//            }
//            return result;
//        }

//        private DBObjectCollection DummySplitCurve(ObjectId objId, IEnumerable<Point3d> points, 
//            BlockTableRecord modelSpace, Transaction transaction, bool selfIntersect)
//        {
//            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
//            if (curve == null)
//                return new DBObjectCollection();

//            var result = new List<ObjectId>();
//            DBObjectCollection spliltCurves = null;
//            if (selfIntersect)
//            {
//                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
//            }
//            else
//            {
//                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
//            }
//            return spliltCurves;
//        }

//        private void GroupIntersectPoints(out Dictionary<ObjectId, List<Point3d>> intersects, 
//            out Dictionary<ObjectId, List<Point3d>> selfIntersects)
//        {
//            intersects = new Dictionary<ObjectId, List<Point3d>>();
//            selfIntersects = new Dictionary<ObjectId, List<Point3d>>();

//            foreach (var crossingInfo in _crossingInfos)
//            {
//                // Self intersection
//                if (crossingInfo.SourceId == crossingInfo.TargetId)
//                {
//                    // Move points from intersects to selfIntersects.
//                    List<Point3d> points = null;
//                    if (intersects.TryGetValue(crossingInfo.SourceId, out points))
//                    {
//                        intersects.Remove(crossingInfo.SourceId);

//                        if (selfIntersects.ContainsKey(crossingInfo.SourceId))
//                        {
//                            var targetPoints = selfIntersects[crossingInfo.SourceId];
//                            foreach (var point in points)
//                            {
//                                if (!targetPoints.Contains(point))
//                                    targetPoints.Add(point);
//                            }
//                        }
//                        else
//                        {
//                            selfIntersects[crossingInfo.SourceId] = points;
//                        }

//                        // Add new ones.
//                        var selfIntersectPoints = selfIntersects[crossingInfo.SourceId];
//                        foreach (var point in crossingInfo.IntersectPoints)
//                        {
//                            if (!selfIntersectPoints.Contains(point))
//                                selfIntersectPoints.Add(point);
//                        }
//                    }
//                    else // Add it to selfIntersects.
//                    {
//                        selfIntersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
//                    }
//                }
//                else // Non-self-intersection
//                {
//                    if (selfIntersects.ContainsKey(crossingInfo.SourceId))
//                    {
//                        var points = selfIntersects[crossingInfo.SourceId];
//                        foreach (var point in crossingInfo.IntersectPoints)
//                        {
//                            if (!points.Contains(point))
//                                points.Add(point);
//                        }
//                    }
//                    else if (!intersects.ContainsKey(crossingInfo.SourceId))
//                    {
//                        intersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
//                    }
//                    else
//                    {
//                        var points = intersects[crossingInfo.SourceId];
//                        foreach (var point in crossingInfo.IntersectPoints)
//                        {
//                            if (!points.Contains(point))
//                                points.Add(point);
//                        }
//                    }

//                    if (selfIntersects.ContainsKey(crossingInfo.TargetId))
//                    {
//                        var points = selfIntersects[crossingInfo.TargetId];
//                        foreach (var point in crossingInfo.IntersectPoints)
//                        {
//                            if (!points.Contains(point))
//                                points.Add(point);
//                        }
//                    }
//                    else if (!intersects.ContainsKey(crossingInfo.TargetId))
//                    {
//                        intersects[crossingInfo.TargetId] = new List<Point3d>(crossingInfo.IntersectPoints);
//                    }
//                    else
//                    {
//                        var points = intersects[crossingInfo.TargetId];
//                        foreach (var point in crossingInfo.IntersectPoints)
//                        {
//                            if (!points.Contains(point))
//                                points.Add(point);
//                        }
//                    }
//                }
//            }
//        }
//    }

    /// <summary>
    /// 
    /// Note : Similar to AutoSplitCurves in FishingBait. But there are some differences in logic.
    /// </summary>
    [Obsolete]
    public class BreakCrossingObjects : AlgorithmWithEditor
    {
        private double _tolerance = 0.01;
        // TODO: Will be updated in future.
        //private List<string> _layers = new List<string>(){"0"}; 

        public BreakCrossingObjects(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// Check results
        /// </summary>
        public IEnumerable<CurveCrossingPoints> CrossingPoints { get; private set; }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();
            CrossingPoints = GetCrossingPointsesForCurves();

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n查找交叉对象花费时间{0}毫秒", elapsedMs);
        }

        public void Fix()
        {
            if (CrossingPoints == null || !CrossingPoints.Any())
                return;

            var database = base.Editor.Document.Database;
            // 通过每条线上的交叉点来分割线
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (var curveCrossingPoints in CrossingPoints)
                {
                    var curve = trans.GetObject(curveCrossingPoints.CurveId, OpenMode.ForWrite) as Curve;
                    if (curve != null)
                    {
                        var sortedPoints = SortBreakPointsByCurve(curve, curveCrossingPoints.Points);
                        var splitedCurves = curve.GetSplitCurves(sortedPoints);
                        // 将分割出来的curves添加到model space.
                        foreach (DBObject splitedCurveObj in splitedCurves)
                        {
                            var splitedCurve = splitedCurveObj as Entity;
                            if(splitedCurve == null)
                                continue;
                            modelSpace.AppendEntity(splitedCurve);
                            trans.AddNewlyCreatedDBObject(splitedCurve, true);
                        }
                        // 删除原始线
                        curve.Erase();
                    }
                }
                trans.Commit();
            }
        }

        private static Point3dCollection SortBreakPointsByCurve(Curve curve, Point3dCollection points)
        {
            var sortedPoints = new Point3dCollection();
            if (curve != null)
            {
                foreach (Point3d point in points)
                {
                    if (sortedPoints.Count == 0)
                    {
                        sortedPoints.Add(point);
                        continue;
                    }
                    var parameter = GeometryUtils.GetPointParameter(curve, point);
                    bool inserted = false;
                    for (int i = 0; i < sortedPoints.Count; i++)
                    {
                        if (GeometryUtils.GetPointParameter(curve, sortedPoints[i]) > parameter)
                        {
                            sortedPoints.Insert(i, point);
                            inserted = true;
                            break;
                        }
                    }

                    // If none of the points in sortedPoints is not larger than point, add point to the tail.
                    if(inserted == false)
                        sortedPoints.Add(point);

                }
            }

            return sortedPoints;
        }


        private IEnumerable<CurveCrossingPoints> GetCrossingPointsesForCurves()
        {
            var curveCrossingPointsList = new List<CurveCrossingPoints>();
            var database = base.Editor.Document.Database;

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var allCurveIds = new ObjectIdCollection();

                foreach (var objectId in modelSpace)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all specified layers curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        //// Check if the curve is from specified layers.
                        //if (_layers.Contains(curve.Layer) == false)
                        //    continue;
                        allCurveIds.Add(objectId);
                    }
                }

                // 遍历所有的objects，对每个object查找其所在bounding box里相交的点。
                foreach (ObjectId curveId in allCurveIds)
                {
                    var currentCurve = trans.GetObject(curveId, OpenMode.ForRead) as Curve;
                    if (currentCurve == null)
                        continue;

                    var allIntersectPoints = new Point3dCollection();
                    var boundingBoxEntityIds = EditorUtils.SelectObjectsByEntityBoundingBox(base.Editor, SelectionFilterUtils.OnlySelectCurve(), currentCurve, _tolerance * 2);

                    var points = new Point3dCollection();
                    foreach (ObjectId boundingBoxEntityId in boundingBoxEntityIds)
                    {
                        var curve = trans.GetObject(boundingBoxEntityId, OpenMode.ForRead) as Curve;
                        if (curve == null || (curve.ObjectId != ObjectId.Null && curve.ObjectId == currentCurve.ObjectId))
                            continue;

                        currentCurve.IntersectWith(curve, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);

                        foreach (Point3d point in points)
                        {
                            allIntersectPoints.Add(point);

                            //// For testing Intersect point.
                            //DBPoint dbPoint = new DBPoint(point);
                            //modelSpace.AppendEntity(dbPoint);
                            //trans.AddNewlyCreatedDBObject(dbPoint, true);
                        }
                    }

                    var crossingPoints = GetCurveIntersectionPoints(currentCurve, allIntersectPoints);
                    if (crossingPoints.Count > 0)
                    {
                        var curveCrossingPoints = new CurveCrossingPoints
                        {
                            CurveId = currentCurve.ObjectId,
                            Points = crossingPoints
                        };
                        curveCrossingPointsList.Add(curveCrossingPoints);
                    }
                }

                trans.Abort();
            }

            return curveCrossingPointsList;
        }

        private static Point3dCollection GetCurveIntersectionPoints(DBObject dbObject, Point3dCollection potentailPoints)
        {
            var splitPoints = new Point3dCollection();

            var curve = dbObject as Curve;
            if (curve == null)
                return splitPoints;

            foreach (Point3d point in potentailPoints)
            {
                // Filter out the point which is at curve's start or end potentailPoints.
                if ((point - curve.StartPoint).Length > Tolerance.Global.EqualPoint &&
                    (point - curve.EndPoint).Length > Tolerance.Global.EqualPoint)
                {
                    bool isPointOnCurve = GeometryUtils.IsPointOnCurveGCP(curve, point);
                    if (isPointOnCurve)
                        splitPoints.Add(point);
                }
            }
            return splitPoints;
        }
    }

    class CurveSegmentForQuadTree : CurveSegment, IQuadObject
    {
        private const double _buffer = 0.01;
        /// <summary>
        /// IQuadObject
        /// </summary>
        public Rect Bounds 
        {
            get
            {
                if(LineSegment == null)
                    return new Rect(0,0,0,0);
                var extents = GetExtents(LineSegment);
                return extents;
            }
        }

        /// <summary>
        /// IQuadObject
        /// </summary>
        public event EventHandler BoundsChanged;

        private static Rect GetExtents(LineSegment2d lineSegment)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            var points = new Point2d[]
            {
                lineSegment.StartPoint, 
                lineSegment.EndPoint
            };
            foreach (var point2D in points)
            {
                minX = Math.Min(minX, point2D.X);
                minY = Math.Min(minY, point2D.Y);
                maxX = Math.Max(maxX, point2D.X);
                maxY = Math.Max(maxY, point2D.Y);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public class BreakCrossingObjectsQuadTree : AlgorithmWithEditor
    {
        private IEnumerable<CurveCrossingInfo> _crossingInfos;
        public IEnumerable<CurveCrossingInfo> CrossingInfos
        {
            get { return _crossingInfos; }
        }

        private IEnumerable<CurveCrossingInfo> _duplicateEntities;
        public IEnumerable<CurveCrossingInfo> DuplicateEntities
        {
            get { return _duplicateEntities; }
        }

        public BreakCrossingObjectsQuadTree(Editor editor)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            // Get intersection info from segmentPairs
            var intersections = SearchRealIntersections(selectedObjectIds, includeInline:true);
            //
            var crossInfos = GetCrossingInfosFromIntersections(intersections);
            _crossingInfos = FilterCrossInfos(crossInfos, out _duplicateEntities);
        }

        public IEnumerable<IntersectionInfo> SearchRealIntersections(IEnumerable<ObjectId> objectIds, bool includeInline)
        {
            if (objectIds == null || !objectIds.Any())
                return new List<IntersectionInfo>();

            // 用碰撞检测的方法查找相邻的线段
            var segments = GetAllCurveSegmentsForQuadTree(objectIds);
            //var quadTree = new QuadTree<CurveSegmentForQuadTree>(new System.Windows.Size(2, 2), 0);
            //// Create quad tree
            //foreach (var segment in segments)
            //    quadTree.Insert(segment);
            var strTree = new STRtree<CurveSegmentForQuadTree>();
            foreach (var segment in segments)
            {
                var envelop = new Envelope(new Coordinate(segment.Bounds.X, segment.Bounds.Y),
                    new Coordinate(segment.Bounds.X + segment.Bounds.Width, segment.Bounds.Y + segment.Bounds.Height));
                strTree.Insert(envelop, segment);
            }

            // Use kd tree to check collision bounding box's intersection
            var segmentPairs = new HashSet<KeyValuePair<CurveSegment, CurveSegment>>();
            var buffer = 0.01;
            foreach (var segment in segments)
            {
                var rect = segment.Bounds;
                //var rectForQuery = new Rect(rect.X - buffer, rect.Y - buffer, rect.Width + buffer, rect.Height + buffer);
                //var nearSegments = quadTree.Query(rectForQuery);
                var envForQuery = new Envelope(new Coordinate(segment.Bounds.X - buffer, segment.Bounds.Y - buffer),
                    new Coordinate(segment.Bounds.X + segment.Bounds.Width + buffer, segment.Bounds.Y + segment.Bounds.Height + buffer));
                var nearSegments = strTree.Query(envForQuery);
                foreach (var nearSegment in nearSegments)
                {
                    if (nearSegment == segment)
                        continue;

                    if (segmentPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(segment, nearSegment)) ||
                        segmentPairs.Contains(new KeyValuePair<CurveSegment, CurveSegment>(nearSegment, segment)))
                        continue;

                    //// 预处理一下，减少计算量
                    //if (IsSegmentOnSameSide(segment.LineSegment, nearSegment.LineSegment))
                    //    continue;

                    segmentPairs.Add(new KeyValuePair<CurveSegment, CurveSegment>(segment, nearSegment));
                }
            }
            // Get intersection info from segmentPairs
            var intersections = GetIntersectionsFromNearPairs(segmentPairs, includeInline);
            return intersections;
        }

        private bool IsSegmentOnSameSide(LineSegment2d source, LineSegment2d target)
        {
            var sourceStart = source.StartPoint;
            var sourceEnd = source.EndPoint;

            var targetStart = target.StartPoint;
            var targetEnd = target.EndPoint;

           
            // Allan: I found IsLeft is not good to determine whether a point is left or right
            // if the segment is too long, so divide it by length.
            var startVal = ComputerGraphics.IsLeft(sourceStart, sourceEnd, targetStart);
            var endVal = ComputerGraphics.IsLeft(sourceStart, sourceEnd, targetEnd);
            if (startVal.Larger(0.0) && endVal.Larger(0.0) ||
                startVal.Smaller(0.0) && endVal.Smaller(0.0))
            {
                return true;
            }
            return false;
        }

        private List<CurveSegmentForQuadTree> GetAllCurveSegmentsForQuadTree(IEnumerable<ObjectId> selectedObjectIds)
        {
            var originSegments = new List<CurveSegmentForQuadTree>();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    var segments = CurveUtils.GetSegment2dsOfCurve(curve, transaction);
                    originSegments.AddRange(segments.Select(it => new CurveSegmentForQuadTree()
                    {
                        LineSegment = it,
                        EntityId = objId
                    }));
                }
                transaction.Commit();
            }
            return originSegments;
        }

        public Dictionary<ObjectId, IEnumerable<ObjectId>> Fix(bool eraseOld)
        {
            var result = new Dictionary<ObjectId, IEnumerable<ObjectId>>();
            if (_crossingInfos == null || !_crossingInfos.Any())
                return result;

            // Group intersect points by ObjectId.
            Dictionary<ObjectId, List<Point3d>> intersects = null;
            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
            GroupIntersectPoints(out intersects, out selfIntersects);

            var document = Editor.Document;
            using (var docLock = document.LockDocument())
            {
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
                    foreach (var pair in intersects)
                    {
                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, false, eraseOld);
                        result.Add(pair.Key, ids);
                    }

                    foreach (var pair in selfIntersects)
                    {
                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, true, eraseOld);
                        result.Add(pair.Key, ids);
                    }

                    transaction.Commit();
                }
            }
            return result;
        }

        public IEnumerable<Entity> DummyFix()
        {
            if (_crossingInfos == null || !_crossingInfos.Any())
                return new Entity[0];

            // Group intersect points by ObjectId.
            Dictionary<ObjectId, List<Point3d>> intersects = null;
            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
            GroupIntersectPoints(out intersects, out selfIntersects);

            var result = new List<Entity>();
            var document = Editor.Document;
            using (var docLock = document.LockDocument())
            {
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
                    foreach (var pair in intersects)
                    {
                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, false);
                        foreach (Entity ent in ents)
                        {
                            result.Add(ent);
                        }
                    }

                    foreach (var pair in selfIntersects)
                    {
                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, true);
                        foreach (Entity ent in ents)
                        {
                            result.Add(ent);
                        }
                    }

                    transaction.Commit();
                }
            }
            return result;
        }

        private IEnumerable<ObjectId> SplitCurve(ObjectId objId, IEnumerable<Point3d> points, BlockTableRecord modelSpace,
            Transaction transaction, bool selfIntersect, bool eraseOld)
        {
            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
            if (curve == null)
                return new ObjectId[0];

            var result = new List<ObjectId>();
            DBObjectCollection spliltCurves = null;
            if (selfIntersect)
            {
                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
            }
            else
            {
                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
            }

            // The splitted curves has the same layer with original curve, 
            // so we needn't set its layer explicitly.
            foreach (Curve splitCurve in spliltCurves)
            {
                var id = modelSpace.AppendEntity(splitCurve);
                transaction.AddNewlyCreatedDBObject(splitCurve, true);
                result.Add(id);
            }

            if (spliltCurves.Count > 0)
            {
                if (eraseOld)
                {
                    // Erase the old one
                    curve.Erase();
                }
            }
            else
            {
                result.Add(curve.Id);
            }
            return result;
        }

        private DBObjectCollection DummySplitCurve(ObjectId objId, IEnumerable<Point3d> points,
            BlockTableRecord modelSpace, Transaction transaction, bool selfIntersect)
        {
            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
            if (curve == null)
                return new DBObjectCollection();

            var result = new List<ObjectId>();
            DBObjectCollection spliltCurves = null;
            if (selfIntersect)
            {
                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
            }
            else
            {
                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
            }
            return spliltCurves;
        }

        private void GroupIntersectPoints(out Dictionary<ObjectId, List<Point3d>> intersects,
            out Dictionary<ObjectId, List<Point3d>> selfIntersects)
        {
            intersects = new Dictionary<ObjectId, List<Point3d>>();
            selfIntersects = new Dictionary<ObjectId, List<Point3d>>();

            foreach (var crossingInfo in _crossingInfos)
            {
                // Self intersection
                if (crossingInfo.SourceId == crossingInfo.TargetId)
                {
                    // Move points from intersects to selfIntersects.
                    List<Point3d> points = null;
                    if (intersects.TryGetValue(crossingInfo.SourceId, out points))
                    {
                        intersects.Remove(crossingInfo.SourceId);

                        if (selfIntersects.ContainsKey(crossingInfo.SourceId))
                        {
                            var targetPoints = selfIntersects[crossingInfo.SourceId];
                            foreach (var point in points)
                            {
                                if (!targetPoints.Contains(point))
                                    targetPoints.Add(point);
                            }
                        }
                        else
                        {
                            selfIntersects[crossingInfo.SourceId] = points;
                        }

                        // Add new ones.
                        var selfIntersectPoints = selfIntersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!selfIntersectPoints.Contains(point))
                                selfIntersectPoints.Add(point);
                        }
                    }
                    else // Add it to selfIntersects.
                    {
                        selfIntersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                }
                else // Non-self-intersection
                {
                    if (selfIntersects.ContainsKey(crossingInfo.SourceId))
                    {
                        var points = selfIntersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                    else if (!intersects.ContainsKey(crossingInfo.SourceId))
                    {
                        intersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                    else
                    {
                        var points = intersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }

                    if (selfIntersects.ContainsKey(crossingInfo.TargetId))
                    {
                        var points = selfIntersects[crossingInfo.TargetId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                    else if (!intersects.ContainsKey(crossingInfo.TargetId))
                    {
                        intersects[crossingInfo.TargetId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                    else
                    {
                        var points = intersects[crossingInfo.TargetId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                }
            }
        }

        private IEnumerable<CurveCrossingInfo> GetCrossingInfosFromIntersections(IEnumerable<IntersectionInfo> infos)
        {
            var result = new List<CurveCrossingInfo>();

            var dictionary = new Dictionary<KeyValuePair<ObjectId, ObjectId>, List<Point3d>>();
            foreach (var info in infos)
            {
                var pair1 = new KeyValuePair<ObjectId, ObjectId>(info.SourceId, info.TargetId);
                var pair2 = new KeyValuePair<ObjectId, ObjectId>(info.TargetId, info.SourceId);
                if (dictionary.ContainsKey(pair1))
                {
                    var val = dictionary[pair1];
                    if (!val.Contains(info.IntersectPoint))
                        val.Add(info.IntersectPoint);
                }
                else if (dictionary.ContainsKey(pair2))
                {
                    var val = dictionary[pair2];
                    if (!val.Contains(info.IntersectPoint))
                        val.Add(info.IntersectPoint);
                }
                else
                {
                    var list = new List<Point3d>();
                    list.Add(info.IntersectPoint);
                    dictionary[pair1] = list;
                }
            }
            foreach (var pair in dictionary)
            {
                var crossingInfo = new CurveCrossingInfo(pair.Key.Key, pair.Key.Value, pair.Value.ToArray());
                result.Add(crossingInfo);
            }
            return result;
        }

        private IEnumerable<CurveCrossingInfo> FilterCrossInfos(IEnumerable<CurveCrossingInfo> crossInfos, out IEnumerable<CurveCrossingInfo> duplicateEntities)
        {
            
            duplicateEntities = new List<CurveCrossingInfo>();
            var result = new List<CurveCrossingInfo>();
            if (crossInfos == null || !crossInfos.Any())
                return result;

            var database = crossInfos.First().SourceId.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var curveCrossingInfo in crossInfos)
                {
                    // Filter out duplicate entities
                    if (AreDuplicateEntities(curveCrossingInfo, transaction))
                    {
                        ((List<CurveCrossingInfo>) duplicateEntities).Add(curveCrossingInfo);
                        continue;
                    }

                    var sourcePoints = CurveUtils.GetCurveEndPoints(curveCrossingInfo.SourceId, transaction);
                    var targetPoints = new Point3d[0];
                    if (curveCrossingInfo.TargetId != curveCrossingInfo.SourceId)
                        targetPoints = CurveUtils.GetCurveEndPoints(curveCrossingInfo.TargetId, transaction);
                    sourcePoints = DistinctEndPoints(sourcePoints);
                    targetPoints = DistinctEndPoints(targetPoints);

                    var points = new List<Point3d>();
                    foreach (var point3D in curveCrossingInfo.IntersectPoints)
                    {
                        // Whether point3D is end point of each cuve

                        // If curveCrossingInfo.SourceId == curveCrossingInfo.TargetId, it's a self intersection.
                        if (sourcePoints.Contains(point3D) && targetPoints.Contains(point3D) &&
                            curveCrossingInfo.SourceId != curveCrossingInfo.TargetId)
                            continue;

                        points.Add(point3D);
                    }
                    if (points.Count > 0)
                    {
                        var newCrossingInfo = new CurveCrossingInfo(curveCrossingInfo.SourceId,
                            curveCrossingInfo.TargetId,
                            points.ToArray());
                        result.Add(newCrossingInfo);
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        private Point3d[] DistinctEndPoints(Point3d[] endPoints)
        {
            if (endPoints.Length != 2)
                return endPoints;
            if (endPoints[0] == endPoints[1])
                return new Point3d[] { endPoints[0] };
            return endPoints;
        }

        private bool AreDuplicateEntities(CurveCrossingInfo crossInfo, Transaction transaction)
        {
            if (crossInfo.SourceId == crossInfo.TargetId)
                return false;

            // Check whether all the intersection points are curve's vertices.
            var sourceVertices = CurveUtils.GetDistinctVertices(crossInfo.SourceId, transaction);
            var targetVertices = CurveUtils.GetDistinctVertices(crossInfo.TargetId, transaction);
            var sourceCount = sourceVertices.Count();
            var targetCount = targetVertices.Count();
            if (sourceCount != targetCount)
                return false;

            if (sourceCount != crossInfo.IntersectPoints.Length)
                return false;

            foreach (var point in crossInfo.IntersectPoints)
            {
                if (!sourceVertices.Contains(point) || !targetVertices.Contains(point))
                    return false;
            }
            return true;
        }

        public static IEnumerable<IntersectionInfo> GetIntersectionsFromNearPairs(IEnumerable<KeyValuePair<CurveSegment, CurveSegment>> segmentPairs, bool includeInline)
        {
            // Use vertexIntersects to record possible self-intersect
            var possibleSelfIntersects = new HashSet<CurveVertex>();
            var result = new List<IntersectionInfo>();
            foreach (var pair in segmentPairs)
            {
                var source = pair.Key;
                var target = pair.Value;

                // 如果共线
                if (source.LineSegment.IsColinearTo(target.LineSegment))
                {
                    if(includeInline)
                        AnalyzeColinearSegments(source, target, result, possibleSelfIntersects);
                }
                else
                {
                    AnalyzeNoneColinearSegments(source, target, result, possibleSelfIntersects);
                }
            }
            return result;
        }

        private static void AnalyzeColinearSegments(CurveSegment source, CurveSegment target,
            List<IntersectionInfo> result, HashSet<CurveVertex> possibleSelfIntersects)
        {
            var sourceSeg = source.LineSegment;
            var targetSeg = target.LineSegment;

            // Analyze targetSeg.StartPoint
            var startParam = sourceSeg.GetParameterOf(targetSeg.StartPoint);
            if (startParam.LargerOrEqual(0.0) && startParam.SmallerOrEqual(1.0))
            {
                var intersectPoint = new Point3d(targetSeg.StartPoint.X, targetSeg.StartPoint.Y, 0.0d);
                if (source.EntityId != target.EntityId) // Id不同
                {
                    result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
                }
                else // Id相同
                {
                    if (startParam.EqualsWithTolerance(0.0) || startParam.EqualsWithTolerance(1.0))
                    {
                        var curveVertex = new CurveVertex(intersectPoint, source.EntityId);
                        if (possibleSelfIntersects.Contains(curveVertex))
                            result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
                        else
                            possibleSelfIntersects.Add(new CurveVertex(intersectPoint, source.EntityId));
                    }
                    else
                        result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId,
                            ExtendType.None, intersectPoint));
                }
            }

            // Analyze targetSeg.EndPoint
            var endParam = sourceSeg.GetParameterOf(targetSeg.EndPoint);
            if (endParam.LargerOrEqual(0.0) && endParam.SmallerOrEqual(1.0))
            {
                var intersectPoint = new Point3d(targetSeg.EndPoint.X, targetSeg.EndPoint.Y, 0.0d);
                if (source.EntityId != target.EntityId) // Id不同
                {
                    result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
                }
                else // Id相同
                {
                    if (endParam.EqualsWithTolerance(0.0) || endParam.EqualsWithTolerance(1.0))
                    {
                        var curveVertex = new CurveVertex(intersectPoint, source.EntityId);
                        if(possibleSelfIntersects.Contains(curveVertex))
                            result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
                        else
                            possibleSelfIntersects.Add(new CurveVertex(intersectPoint, source.EntityId));
                    }
                    else
                    {
                        result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId,
                            ExtendType.None, intersectPoint));
                    }
                }
            }

            // Analyze sourceSeg.StartPoint
            startParam = targetSeg.GetParameterOf(sourceSeg.StartPoint);
            if (startParam.Larger(0.0) && startParam.Smaller(1.0))
            {
                var intersectPoint = new Point3d(sourceSeg.StartPoint.X, sourceSeg.StartPoint.Y, 0.0d);
                result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
            }

            endParam = targetSeg.GetParameterOf(sourceSeg.EndPoint);
            if (endParam.Larger(0.0) && endParam.Smaller(1.0))
            {
                var intersectPoint = new Point3d(sourceSeg.EndPoint.X, sourceSeg.EndPoint.Y, 0.0d);
                result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint));
            }
        }

        private static void AnalyzeNoneColinearSegments(CurveSegment source, CurveSegment target,
            List<IntersectionInfo> result, HashSet<CurveVertex> possibleSelfIntersects)
        {
            var sourceSeg = source.LineSegment;
            var targetSeg = target.LineSegment;

            // 不共线的情况
            var points = sourceSeg.IntersectWith(targetSeg);
            if (points == null || points.Length <= 0)
                return;

            var intersectPoint = points[0];
            var intersectPoint3D = new Point3d(intersectPoint.X, intersectPoint.Y, 0.0d);

            // 如果不在端点相交，肯定是交点
            if (!intersectPoint.IsEqualTo(sourceSeg.StartPoint) && !intersectPoint.IsEqualTo(sourceSeg.EndPoint) ||
                !intersectPoint.IsEqualTo(targetSeg.StartPoint) && !intersectPoint.IsEqualTo(targetSeg.EndPoint))
            {
                result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint3D));
            }
            else // 相交在端点
            {
                if (source.EntityId != target.EntityId)
                {
                    // 后期会统一处理，如果交点是线的端点的话，则需要filter out
                    result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint3D));
                }
                else
                {
                    // 自相交点
                    var curveVertex = new CurveVertex(intersectPoint3D, source.EntityId);
                    if (possibleSelfIntersects.Contains(curveVertex))
                        result.Add(new IntersectionInfo(source.EntityId, ExtendType.None, target.EntityId, ExtendType.None, intersectPoint3D));
                    else
                        possibleSelfIntersects.Add(new CurveVertex(intersectPoint3D, source.EntityId));
                }
            }
        }
    }
}
