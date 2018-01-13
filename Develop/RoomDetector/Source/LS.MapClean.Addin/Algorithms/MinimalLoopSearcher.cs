using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ClipperLib;
using GeoAPI.Geometries;
using LS.MapClean.Addin.Utils;
using NetTopologySuite.Index.Quadtree;
using QuickGraph;
using TopologyTools.ReaderWriter;
using TopologyTools.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public class MinimalLoopSearcher : IDisposable
    {
        //private IEnumerable<Loop> _minimalLoops;
        //public IEnumerable<Loop> MinimalLoops
        //{
        //    get { return _minimalLoops; }
        //}

        private IEnumerable<ObjectId> _idsInLoop;
        public IEnumerable<ObjectId> IdsInLoop
        {
            get { return _idsInLoop; }
        }

        private IEnumerable<Region> _regions;
        public IEnumerable<Region> Regions
        {
            get { return _regions; }
        }

        private Transaction _transaction;
        public MinimalLoopSearcher(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException("transaction");
            _transaction = transaction;
        }

        public void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var watch = Stopwatch.StartNew();
            // Build a quick graph by using KdTreeCurveGraphBuilder
            var graphBuilderKdTree = new KdTreeCurveGraphBuilder(selectedObjectIds, _transaction, includeSelfLoop: true);
            graphBuilderKdTree.BuildGraph();

            // Search all loops
            var paths = graphBuilderKdTree.SearchLoops();
            //_minimalLoops = PostProcessLoops(loops);
            var ids = new HashSet<ObjectId>();
            var regions = new List<Region>();
            foreach (var path in paths)
            {
                var loop = new Loop(path);
                // Region sometimes couldn't be created, don't know the reason.
                var region = loop.CreateRegion(_transaction);
                if (region == null)
                    continue;

                var loopIds = loop.IdsInLoop();
                foreach (var id in loopIds)
                {
                    ids.Add(id);
                }
                regions.Add(region);
            }
            _idsInLoop = ids;
            _regions = BoolOperationRegions(regions);

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            System.Diagnostics.Debug.WriteLine("搜索闭环花费{0}毫秒", elapsedMs);
        }

        private List<Region> BoolOperationRegions(List<Region> regions)
        {
            if (regions.Count == 0 || regions.Count == 1)
                return regions;

            var results = new List<Region>();
            results.Add(regions[0]);
            regions.RemoveAt(0);

            while (regions.Count > 0)
            {
                var right = regions[0];
                var newRegions = new List<Region>();
                foreach (var left in results)
                {
                    var newRegion = BoolOperationRegions(left, right);
                    if (newRegion != null)
                        newRegions.Add(newRegion);
                }
                
                results.Add(right);
                results.AddRange(newRegions);
                regions.RemoveAt(0);
            }
            return results;
        }

        //private void BoolOperationRegions(Region left, IEnumerable<Region> rights, List<Region> results )
        //{
        //    if (!rights.Any())
        //        return;
        //    var right = rights.First();
        //    var newRegion = BoolOperationRegions(left, right);
        //    if (newRegion != null)
        //    {
        //        results.Add(newRegion);
        //    }

        //    var newRights = rights.Except(new Region[] {right});
        //    foreach (var newLeft in results)
        //    {
        //        BoolOperationRegions(newLeft, newRights, results);
        //    }
        //}

        /// <summary>
        /// Return the new region of intersection.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        private Region BoolOperationRegions(Region left, Region right)
        {
            if (!IsBoundingBoxIntersect(left, right))
            {
                return null;
            }

            Region result = null;
            var bigRegion = left;
            var smallRegion = right;
            bool leftBigger = true;
            if (left.Area.Smaller(right.Area))
            {
                bigRegion = right;
                smallRegion = left;
                leftBigger = false;
            }

            var bigClone = bigRegion.Clone() as Region;
            var smallClone = smallRegion.Clone() as Region;
            bigClone.BooleanOperation(BooleanOperationType.BoolIntersect, smallClone);
            if (bigClone.Area.EqualsWithTolerance(smallRegion.Area, 0.0005)) // 包含关系
            {
                bigRegion.BooleanOperation(BooleanOperationType.BoolSubtract, bigClone);
                bigClone.Dispose();
                smallClone.Dispose();
            }
            else if (bigClone.Area.EqualsWithTolerance(0.0) || 
                bigClone.Area.EqualsWithTolerance(bigRegion.Area)) // No intersect
            {
                bigClone.Dispose();
                smallClone.Dispose();
            }
            else // intersect
            {
                var newClone1 = bigClone.Clone() as Region;
                var newClone2 = bigClone.Clone() as Region;
                bigRegion.BooleanOperation(BooleanOperationType.BoolSubtract, newClone1);
                smallRegion.BooleanOperation(BooleanOperationType.BoolSubtract, newClone2);
                result = bigClone; // Intersected region

                newClone1.Dispose();
                newClone2.Dispose();
                smallClone.Dispose();
            }
            return result;
        }

        private bool IsBoundingBoxIntersect(Region left, Region right)
        {
            var leftExtent = left.GeometricExtents;
            var rightExtent = right.GeometricExtents;

            if (leftExtent.MinPoint.X > rightExtent.MaxPoint.X ||
                leftExtent.MinPoint.Y > rightExtent.MaxPoint.Y ||
                leftExtent.MaxPoint.X < rightExtent.MinPoint.X ||
                leftExtent.MaxPoint.Y < rightExtent.MinPoint.Y)
            {
                return false;
            }
            return true;
        }
        //private IEnumerable<Loop> PostProcessLoops(IEnumerable<SEdge<CurveVertex>[]> paths)
        //{
        //    var loops = new List<Loop>();
        //    // Initialize loops
        //    foreach (var path in paths)
        //    {
        //        loops.Add(new Loop(path));
        //    }
        //    var count = loops.Count;
        //    for (int i = 0; i < count - 1; i++)
        //    { 
        //        for (int j = i + 1; j < count; j++)
        //        {
        //            var newLoops = Loop.BooleanOperation(loops[i], loops[j]);
        //            if (newLoops.Count != 2)
        //                continue;
        //            if (newLoops[0].Id != loops[i].Id)
        //            {
        //                loops[i].Dispose();
        //                loops[i] = newLoops[0];
        //            }
        //            if (newLoops[1].Id != loops[j].Id)
        //            {
        //                loops[j].Dispose();
        //                loops[j] = newLoops[1];
        //            }
        //        }
        //    }
            
        //    return loops;
        //}

        //public void Dispose()
        //{
        //    if (_minimalLoops != null)
        //    {
        //        foreach (var minimalLoop in _minimalLoops)
        //        {
        //            minimalLoop.Dispose();
        //        }
        //        _minimalLoops = null;
        //    }
            
        //}

        public void Dispose()
        {
            if (_regions != null)
            {
                foreach (var region in Regions)
                {
                    region.Dispose();
                }
                _regions = null;
            }
        }
    }

    public class Loop
    {
        /// <summary>
        /// Vertices
        /// </summary>
        //private List<CurveVertex> _vertices = new List<CurveVertex>();
        //public List<CurveVertex> Vertices
        //{
        //    get { return _vertices; }
        //}

        private IEnumerable<SEdge<CurveVertex>> _edges = null;

        public Loop(IEnumerable<SEdge<CurveVertex>> edges)
        {
            _edges = edges;
        }

        public Region CreateRegion(Transaction transaction)
        {
            var ids = IdsInLoop();
            if (!ids.Any())
                return null;

            Region result = null;
            try
            {
                var linearObjects = new DBObjectCollection();
                foreach (var objectId in ids)
                {
                    var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
                    if (entity is Line || entity is Arc || entity is Polyline || entity is Polyline2d)
                    {
                        linearObjects.Add(entity);
                    }
                }

                var regions = Region.CreateFromCurves(linearObjects);
                // I'm sure there is only one region.
                if (regions.Count > 0)
                    result = (Region)regions[0];
#if DEBUG
                if (regions.Count != 1)
                {
                    System.Diagnostics.Debug.WriteLine("Error: {0} regions are created", regions.Count);
                }
#endif
                foreach (DBObject linearObject in linearObjects)
                {
                    linearObject.Dispose();
                }
                linearObjects.Dispose();
            }
            catch (Exception)
            {

            }
            return result;
        }


        public IEnumerable<ObjectId> IdsInLoop()
        {
            var result = new HashSet<ObjectId>();
            foreach (var sEdge in _edges)
            {
                if (sEdge.Source.Id != sEdge.Target.Id)
                    continue;
                result.Add(sEdge.Source.Id);
            }
            return result;
        }

    }

//    public class Loop : IDisposable
//    {
//        public Guid Id { get; private set; }

//        /// <summary>
//        /// Vertices
//        /// </summary>
//        private List<CurveVertex> _vertices = new List<CurveVertex>();
//        public List<CurveVertex> Vertices
//        {
//            get { return _vertices; }
//        }

//        /// <summary>
//        /// Region created by edges.
//        /// </summary>
//        private Region _region = null;
//        public Region Region
//        {
//            get
//            {
//                if (_region == null)
//                {
//                    try
//                    {
//                        CreateRegion();
//                    }
//                    catch
//                    {
//                    }
//                }
//                return _region;
//            }
//        }

//        public Loop()
//        {
//            Id = Guid.NewGuid();
//        }

//        public Loop(IEnumerable<SEdge<CurveVertex>> edges)
//            : this()
//        {
//            foreach (var sEdge in edges)
//            {
//                // Every loop start from target to source.
//                if (!_vertices.Contains(sEdge.Target))
//                    _vertices.Add(sEdge.Target);
//                if (!_vertices.Contains(sEdge.Source))
//                    _vertices.Add(sEdge.Source);
//            }
//        }

//        private void CreateRegion()
//        {
//            var ids = IdsInLoop();
//            if (!ids.Any())
//                return;

//            var database = ids.First().Database;
//            using (var transaction = database.TransactionManager.StartTransaction())
//            {
//                var linearObjects = new DBObjectCollection();
//                foreach (var objectId in ids)
//                {
//                    var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
//                    if (entity is Line || entity is Arc || entity is Polyline || entity is Polyline2d)
//                    {
//                        linearObjects.Add(entity);
//                    }
//                }

//                var regions = Region.CreateFromCurves(linearObjects);
//                // I'm sure there is only one region.
//                if (regions.Count > 0)
//                    _region = (Region)regions[0];
//#if DEBUG
//                if (regions.Count != 1)
//                {
//                    System.Diagnostics.Debug.WriteLine("Error: {0} regions are created", regions.Count);
//                }
//#endif

//                transaction.Commit();
//            }
//        }

//        public IEnumerable<Point3d> PointsOfLoop()
//        {
//            var result = new List<Point3d>();
//            foreach (var vertex in _vertices)
//            {
//                // If edge is reversed, target and source are reverse.
//                if (!result.Contains(vertex.Point))
//                    result.Add(vertex.Point);
//            }
//            return result;
//        }

//        public void Dispose()
//        {
//            if (_region != null)
//                _region.Dispose();
//            _region = null;
//        }

//        public IEnumerable<ObjectId> IdsInLoop()
//        {
//            var groups = _vertices.GroupBy(x => x.Id)
//                .Where(x => x.Count() > 1);
//            return groups.Select(it => it.Key).ToList();
//        }

//        #region Static methods
//        public static List<Loop> BooleanOperation(Loop left, Loop right)
//        {
//            var result = new List<Loop>();

//            // Check region first
//            var leftRegion = left.Region;
//            var rightRegion = right.Region;

//            if (leftRegion == null || rightRegion == null)
//            {
//                if (rightRegion != null)
//                    result.Add(right);

//                if (leftRegion != null)
//                    result.Add(left);
//                return result;
//            }

//            // If they are not intersect.
//            var intersects = left.Vertices.Intersect(right.Vertices);
//            if (!intersects.Any())
//            {
//                result.Add(left);
//                result.Add(right);
//                return result;
//            }

//            // Boolean operation
//            var bigArea = leftRegion.Area;
//            var smallArea = rightRegion.Area;
//            var loopBig = left;
//            var loopSmall = right;
//            bool leftIsBigger = true;
//            if (bigArea.Smaller(smallArea))
//            {
//                loopBig = right;
//                loopSmall = left;
//                bigArea = rightRegion.Area;
//                smallArea = leftRegion.Area;
//                leftIsBigger = false;
//            }

//            var bigRegionClone = loopBig.Region.Clone() as Region;
//            var smallRegionClone = loopSmall.Region.Clone() as Region;

//            bigRegionClone.BooleanOperation(BooleanOperationType.BoolIntersect, smallRegionClone);
//            var sum = bigRegionClone.Area - smallArea;
//            // loopSmall is inside of loopBig.
//            if (sum.EqualsWithTolerance(0.0))
//            {
//                var insertVertices = loopSmall.Vertices.Except(loopBig.Vertices).ToList();
//                var exceptVertices = loopBig.Vertices.Intersect(loopSmall.Vertices).ToList();
//                if (exceptVertices.Count > 0)
//                {
//                    // The first and last vertices won't be removed.
//                    exceptVertices.RemoveAt(0);
//                    if (exceptVertices.Count > 0)
//                    {
//                        exceptVertices.RemoveAt(exceptVertices.Count -1);
//                    }
//                }

//                var newVertices = loopBig.Vertices.ToList();
//                var index = 0;
//                if (exceptVertices.Count > 0)
//                {
//                    index = newVertices.IndexOf(exceptVertices[0]);
//                    newVertices.RemoveRange(index, exceptVertices.Count);
//                }
//                insertVertices.Reverse();
//                newVertices.InsertRange(index, insertVertices);
                
//                var newLoop = new Loop();
//                newLoop.Vertices.AddRange(newVertices);

//                if (leftIsBigger)
//                {
//                    result.Add(newLoop);
//                    result.Add(loopSmall);
//                }
//                else
//                {
//                    result.Add(loopSmall);
//                    result.Add(newLoop);
//                }
//            }
//            else if (bigRegionClone.Area.Smaller(bigArea))
//            {
//                // loopBig and loopSmall are intersect
                
//            }
//            else
//            {
//                result.Add(left);
//                result.Add(right);
//            }

//            bigRegionClone.Dispose();
//            smallRegionClone.Dispose();
//            return result;
//        }

//        #endregion
//    }

    public class MinimalLoopSearcher2
    {
        //
        public static IEnumerable<Polyline> Search(IEnumerable<ObjectId> selectedObjectIds, Document document)
        {
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return new Polyline[0];
#if DEBUG
            var watch = Stopwatch.StartNew();
#endif
            IEnumerable<SEdge<CurveVertex>[]> loops = null;
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                // Build a quick graph by using KdTreeCurveGraphBuilder
                var graphBuilderKdTree = new KdTreeCurveGraphBuilder(selectedObjectIds, transaction, includeSelfLoop: true);
                graphBuilderKdTree.BuildGraph();

                // Search all loops
                loops = graphBuilderKdTree.SearchLoops();

                transaction.Commit();
            }

            if (loops == null || !loops.Any())
                return new Polyline[0];

            // Use clipper to search all minimum polygons.
            var polylines = CreatePolylines(loops, document);
            if(polylines == null || !polylines.Any())
                return new Polyline[0];

            Dictionary<Curve, List<Curve>> replaced = null;
            var polygons = SearchMinimalPolygons(polylines, out replaced);

            // Dispose polylines
            var allPolylines = new HashSet<Curve>();
            if (replaced != null)
            {
                foreach (var pair in replaced)
                {
                    allPolylines.Add(pair.Key);
                    foreach (var polyline in pair.Value)
                    {
                        allPolylines.Add(polyline);
                    }
                }
            }
            var diffs = allPolylines.Except(polygons);
            foreach (var polyline in diffs)
            {
                polyline.Dispose();
            }
            
#if DEBUG
            watch.Stop();
            var elapseMs = watch.ElapsedMilliseconds;
            var editor = document.Editor;
            editor.WriteMessage("\n造封闭多边形花费{0}毫秒\n", elapseMs);
#endif
            return polygons.Cast<Polyline>();
        }

        private static IEnumerable<Polyline> CreatePolylines(IEnumerable<SEdge<CurveVertex>[]> loops, Document document)
        {
            var result = new List<Polyline>();
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (var loop in loops)
                {
                    var polylines = CreatePolylines2(loop, transaction);
                    result.AddRange(polylines);
                }
                transaction.Commit();
            }
            return result;
        }

        private static IEnumerable<SEdge<CurveVertex>[]> SplitLoop(IEnumerable<SEdge<CurveVertex>> loop)
        {
            var result = new List<SEdge<CurveVertex>[]>();
            var stack = new Stack<SEdge<CurveVertex>>();
            var prevId = ObjectId.Null;
            foreach (var edge in loop)
            {
                if (edge.Source.Id != edge.Target.Id)
                    continue;

                // 防止中点参与计算
                if (edge.Source.Id != prevId)
                {
                    prevId = edge.Source.Id;
                    stack.Push(edge);
                    continue;
                }

                var existing = stack.FirstOrDefault(it => it.Target.Point == edge.Source.Point);
                if (existing.Equals(default(SEdge<CurveVertex>)))
                {
                    stack.Push(edge);
                    continue;
                }

                var split = new List<SEdge<CurveVertex>>();
                split.Add(edge);
                while (true)
                {
                    var pop = stack.Pop();
                    split.Insert(0, pop);
                    if (pop.Equals(existing))
                        break;
                }
                result.Add(split.ToArray());
            }
            return result;
        }


        private static Polyline CreatePolyline2(SEdge<CurveVertex>[] loop, Transaction transaction)
        {
            if (loop == null || loop.Length <= 0)
                return null;

            var prevId = ObjectId.Null;
            var vertices = new List<Point3d>();
            for (int i = 0; i < loop.Length; i++)
            {
                var sEdge = loop[i];
                if (sEdge.Source.Id != sEdge.Target.Id || sEdge.Source.Id == prevId)
                    continue;
                var tempVertices = CurveUtils.GetDistinctVertices(sEdge.Source.Id, transaction);
                if (tempVertices.Count <= 0)
                    continue;

                prevId = sEdge.Source.Id;
                // 如果图的边的target点不是曲线的首点，那么反转
                // 我们得到的loop本身就是逆序的，是target->source->target->source样子的，所以用target来比较
                if (sEdge.Target.Point != tempVertices[0])
                    tempVertices.Reverse();

                // 防止重点
                if(vertices.Count > 0)
                    tempVertices.RemoveAt(0);
                vertices.AddRange(tempVertices);
            }
            var polyline = CurveUtils.CreatePolygon(vertices.ToArray());
            return polyline;
        }

        private static IEnumerable<Polyline> CreatePolylines2(SEdge<CurveVertex>[] loop, Transaction transaction)
        {
            var result = new List<Polyline>();
            if (loop == null || loop.Length <= 0)
                return result;

            // 将大的loop分解成小的loop，避免自相交
            var unitLoops = SplitLoop(loop);
            foreach (var unitLoop in unitLoops)
            {
                var polyline = CreatePolyline2(unitLoop, transaction);
                if(polyline != null)
                    result.Add(polyline);
            }
            return result;
        }

        private static IEnumerable<Polyline> CreatePolylines(SEdge<CurveVertex>[] loop, Transaction transaction)
        {
            var result = new List<Polyline>();
            if (loop == null || loop.Length <= 0)
                return result;

            var lastId = ObjectId.Null;
            for (int i = 0; i < loop.Length; i++)
            {
                var sourceId = loop[i].Source.Id;
                var targetId = loop[i].Target.Id;
                // Only when sourceId is equal to targetId
                if (sourceId != targetId)
                    continue;

                lastId = sourceId;
            }

            // 用我们的KdTreeCurveGraphBuilder可能会产生在首点自相交的多边形，
            // 解决这个问题, 要记录第一个顶点
            var firstPoint = loop[0].Source.Point;
            var prevId = ObjectId.Null;
            var vertices = new List<Point3d>();
            for (int i = 0; i < loop.Length; i++)
            {
                var sEdge = loop[i];
                if (sEdge.Source.Id != sEdge.Target.Id || sEdge.Source.Id == prevId)
                    continue;
                var tempVertices = CurveUtils.GetDistinctVertices(sEdge.Source.Id, transaction);
                if (tempVertices.Count <= 0)
                    continue;

                prevId = sEdge.Source.Id;
                // 如果图的边的target点不是曲线的首点，那么反转
                // 我们得到的loop本身就是逆序的，是target->source->target->source样子的，所以用target来比较
                if (sEdge.Target.Point != tempVertices[0])
                    tempVertices.Reverse();
                vertices.AddRange(tempVertices);
                if (vertices[vertices.Count - 1] == firstPoint || sEdge.Target.Id == lastId)
                {
                    // 如果vertices最后的点等于第一个点，那么生成polygon
                    var distincts = CurveUtils.GetDistinctVertices(vertices);
                    var polyline = CurveUtils.CreatePolygon(distincts.ToArray());
                    result.Add(polyline);
                    // 如果还有边，那么要继续下去
                    if (sEdge.Target.Id != lastId)
                        vertices = new List<Point3d>();
                }
            }
            return result;
        }

        private static Polyline CreatePolyline(SEdge<CurveVertex>[] loop, Transaction transaction)
        {
            if (loop == null || loop.Length <= 0)
                return null;

            var ids = new List<ObjectId>();
            for(int i = 0; i < loop.Length; i++)
            {
                var sourceId = loop[i].Source.Id;
                var targetId = loop[i].Target.Id;
                // Only when sourceId is equal to targetId
                if (sourceId != targetId)
                    continue;
                // 保证id不重复
                if(!ids.Contains(sourceId))
                    ids.Add(sourceId);
            }

            List<Point3d> vertices = null;
            foreach (var objectId in ids)
            {
                var tempVertices = CurveUtils.GetDistinctVertices(objectId, transaction);
                if (tempVertices.Count <= 0)
                    continue;

                if (vertices == null)
                {
                    vertices = tempVertices;
                    continue;
                }
                // tempVertices是否需要反转？
                if (vertices[vertices.Count - 1] == tempVertices[tempVertices.Count - 1] ||
                    vertices[0] == tempVertices[tempVertices.Count - 1])
                {
                    tempVertices.Reverse();
                }

                if (vertices[0] == tempVertices[0])
                    vertices.Reverse();

                vertices.AddRange(tempVertices);
            }
            if (vertices == null || vertices.Count <= 0)
                return null;

            var polyline = CurveUtils.CreatePolygon(vertices.ToArray());
            return polyline;
        }

        public static IEnumerable<Polyline> GetAllLoopPolylines(IEnumerable<ObjectId> selectedObjectIds, Document document)
        {
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return new Polyline[0];

            IEnumerable<SEdge<CurveVertex>[]> loops = null;
            using(var tolerance = new SafeToleranceOverride())
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                // Build a quick graph by using KdTreeCurveGraphBuilder
                var graphBuilderKdTree = new KdTreeCurveGraphBuilder(selectedObjectIds, transaction, includeSelfLoop: true);
                graphBuilderKdTree.BuildGraph();

                // Search all loops
                loops = graphBuilderKdTree.SearchLoops();

                transaction.Commit();
            }

            if (loops == null || !loops.Any())
                return new Polyline[0];

            // Use clipper to search all minimum polygons.
            var polylines = CreatePolylines(loops, document);
            if (polylines == null || !polylines.Any())
                return new Polyline[0];

            return polylines;
        }

        public static IEnumerable<Curve> SearchMinimalPolygons(IEnumerable<Curve> polylines, out Dictionary<Curve, List<Curve>> replaced)
        {
            // 1. Create a kd tree for all polyline vertices
            var polylineVertices = new List<PolylineVertex>();
            foreach (var polyline in polylines)
            {
                var vertices = CurveUtils.GetDistinctVertices(polyline, null);
                var pvs = vertices.Select(it => new PolylineVertex(it, polyline));
                polylineVertices.AddRange(pvs);
            }

            var kdTree = new CurveVertexKdTree<PolylineVertex>(polylineVertices, it => it.Point.ToArray(), ignoreZ: true);

            // 2. Analyze polygons
            var result = new List<Curve>();
            var visited = new HashSet<Curve>();
            replaced = new Dictionary<Curve, List<Curve>>();
            // 按由大到小来，否则某些重叠解决不了
            polylines = polylines.OrderByDescending(it => it.Area).ToList();
            foreach (var polyline in polylines)
            {
                // 现在此处加上visited，否则GetNearPolylines有可能查到polyline
                visited.Add(polyline);
                var replacedPls = GetReplacedPolylines(polyline, replaced);
                replacedPls = replacedPls.OrderByDescending(it => it.Area).ToList();
                foreach (var replacedPl in replacedPls)
                {
                    var nearPolylines = GetNearPolylines(replacedPl, kdTree, replaced, visited);
                    var polygons = AnalyzePolygons2(replacedPl, nearPolylines, replaced);
                    result.AddRange(polygons);
                }
            }
            result = ResolveIncludePolylines(result);
            return result;
        }

        private static IEnumerable<Curve> GetNearPolylines(Curve polyline, CurveVertexKdTree<PolylineVertex> kdTree,
            Dictionary<Curve, List<Curve>> replaced, HashSet<Curve> visited)
        {
            var result = new HashSet<Curve>();
            var extents = polyline.GeometricExtents;
            var nearVertices = kdTree.BoxedRange(extents.MinPoint.ToArray(), extents.MaxPoint.ToArray());
            foreach (var polylineVertex in nearVertices)
            {
                if (polylineVertex.Polyline == polyline || visited.Contains(polylineVertex.Polyline)
                    || result.Contains(polylineVertex.Polyline))
                {
                    continue;
                }

                var pls = GetReplacedPolylines(polylineVertex.Polyline, replaced);
                foreach (var pl in pls)
                {
                    if(!result.Contains(pl))
                        result.Add(pl);
                }
            }
            return result;
        }

        public static IEnumerable<Curve> SearchMinimalPolygons2(IEnumerable<Curve> polylines, out Dictionary<Curve, List<Curve>> replaced)
        {
            // 1. Create a quadtree for all polyline vertices
            var allNears = NtsUtils.GetNearGeometries(polylines, 0.1, forceClosed: true);

            // 2. Analyze polygons
            var result = new List<Curve>();
            var visited = new HashSet<Curve>();
            replaced = new Dictionary<Curve, List<Curve>>();
            // 按由大到小来，否则某些重叠解决不了
            polylines = polylines.OrderByDescending(it => it.Area).ToList();
            foreach (var polyline in polylines)
            {
                // 现在此处加上visited，否则GetNearPolylines有可能查到polyline
                visited.Add(polyline);
                var replacedPls = GetReplacedPolylines(polyline, replaced);
                replacedPls = replacedPls.OrderByDescending(it => it.Area).ToList();
                foreach (var replacedPl in replacedPls)
                {
                    var nearPolylines = GetNearPolylines2(replacedPl, allNears, replaced, visited);
                    var polygons = AnalyzePolygons2(replacedPl, nearPolylines, replaced);
                    result.AddRange(polygons);
                }
            }

            return result;
        }

        private static IEnumerable<Curve> GetNearPolylines2(Curve polyline, Dictionary<Curve, IList<Curve>> allNears,
            Dictionary<Curve, List<Curve>> replaced, HashSet<Curve> visited)
        {
            var result = new HashSet<Curve>();
            if (!allNears.ContainsKey(polyline))
                return result;

            var nears = allNears[polyline];
            foreach (Curve near in nears)
            {
                if (visited.Contains(near) || result.Contains(near))
                {
                    continue;
                }

                var pls = GetReplacedPolylines(near, replaced);
                foreach (var pl in pls)
                {
                    if (!result.Contains(pl))
                        result.Add(pl);
                }
            }
            return result;
        }

        private static IEnumerable<Curve> GetReplacedPolylines(Curve polyline,
            Dictionary<Curve, List<Curve>> replaced)
        {
            var result = new List<Curve>();
            if(!replaced.ContainsKey(polyline))
                result.Add(polyline);
            else
            {
                var replacedPls = replaced[polyline];
                // replacedPls如果为空，意味着polyline在计算过程中消亡
                foreach (var replacedPl in replacedPls)
                {
                    result.AddRange(GetReplacedPolylines(replacedPl, replaced));
                }
            }
            return result;
        }

        private static IEnumerable<Curve> AnalyzePolygons(Curve polyline, IEnumerable<Curve> nearPolylines,
            Dictionary<Curve, List<Curve>> replaced)
        {
            var sourcePolylines = new List<Curve>() { polyline };
            foreach (var nearPolyline in nearPolylines)
            {
                var newSourcePolylines = new List<Curve>();
                // sourcePolylines里面的每条线都是不相交的。
                foreach (var sourcePolyline in sourcePolylines)
                {
                    // 每次循环nearPolyline会发生变化
                    var newNearPolylines = GetReplacedPolylines(nearPolyline, replaced);
                    if (newNearPolylines != null && newNearPolylines.Any())
                    {
                        foreach (var newNearPolyline in newNearPolylines)
                        {
                            List<Curve> intersects, sourceDiffs, targetDiffs;
                            BooleanOperation(sourcePolyline, newNearPolyline, out intersects, out sourceDiffs, out targetDiffs);
                            if (intersects != null && intersects.Count > 0)
                            {
                                newSourcePolylines.AddRange(intersects);
                                // 如果intersects不为空， sourceDiffs有可能为空，但不会为null
                                newSourcePolylines.AddRange(sourceDiffs);
                            }
                            else
                            {
                                newSourcePolylines.Add(sourcePolyline);
                            }

                            // 将nearPolyline替换
                            if (intersects != null && intersects.Count > 0)
                            {
                                // targetDiffs 有可能为空，但不会为null
                                replaced[newNearPolyline] = targetDiffs;
                            }
                        }
                    }

                    else
                    {
                        // 如果不存在near polyline，则sourcePolyline要添加进去
                        newSourcePolylines.Add(sourcePolyline);
                    }
                    
                }
                sourcePolylines = newSourcePolylines;
            }
            return sourcePolylines;
        }

        private static IEnumerable<Curve> AnalyzePolygons2(Curve polyline, IEnumerable<Curve> nearPolylines,
            Dictionary<Curve, List<Curve>> replaced)
        {
            var sourceReplaced = new Dictionary<Curve, List<Curve>>();
            foreach (var nearPolyline in nearPolylines)
            {
                var replacedSourcePolylines = GetReplacedPolylines(polyline, sourceReplaced);
                // replacedSourcePolylines里面的每条线都是不相交的。
                foreach (var sourcePolyline in replacedSourcePolylines)
                {
                    // 每次循环nearPolyline会发生变化
                    var newNearPolylines = GetReplacedPolylines(nearPolyline, replaced);
                    foreach (var newNearPolyline in newNearPolylines)
                    {
                        List<Curve> intersects, sourceDiffs, targetDiffs;
                        BooleanOperation(sourcePolyline, newNearPolyline, out intersects, out sourceDiffs, out targetDiffs);
                        if (intersects != null && intersects.Count > 0)
                        {
                            // 如果intersects不为空， sourceDiffs有可能为空，但不会为null
                            sourceReplaced[sourcePolyline] = sourceDiffs;
                            // 将nearPolyline替换
                            replaced[newNearPolyline] = intersects;
                            // targetDiffs 有可能为空，但不会为null,空意味着这条线不会再在以后的计算中发挥作用了
                            replaced[newNearPolyline].AddRange(targetDiffs);
                        }
                    }
                    
                }
            }

            var results = GetReplacedPolylines(polyline, sourceReplaced);
            return results;
        }

        private static IEnumerable<Curve> AnalyzePolygonAtom(Curve polyline, Curve nearPolyline, Dictionary<Curve, List<Curve>> replaced)
        {
            var result = new List<Curve>();
            List<Curve> intersects, sourceDiffs, targetDiffs;
            BooleanOperation(polyline, nearPolyline, out intersects, out sourceDiffs, out targetDiffs);
            if (intersects != null && intersects.Count > 0)
            {
                result.AddRange(intersects);
                // 如果intersects不为空， sourceDiffs有可能为空，但不会为null
                result.AddRange(sourceDiffs);
            }
            else
            {
                result.Add(polyline);
            }

            // 将nearPolyline替换
            if (intersects != null && intersects.Count > 0)
            {
                // targetDiffs 有可能为空，但不会为null
                replaced[nearPolyline] = targetDiffs;
            }
            return result;
        }

        private static void BooleanOperation(Curve source, Curve target, out List<Curve> intersects,
            out List<Curve> sourceDiffs, out List<Curve> targetDiffs)
        {
            intersects = null;
            sourceDiffs = null;
            targetDiffs = null;

            // 获取相交部分
            var tempIntersects = ClipperBoolean(new List<Curve>() { source }, new List<Curve>() { target }, ClipType.ctIntersection);
            if (tempIntersects.Count <= 0)
                return;
            intersects = new List<Curve>(tempIntersects);

            // 获取source polyline和相交部分的差
            var tempSourceDiffs = ClipperBoolean(new List<Curve>() { source }, tempIntersects, ClipType.ctDifference);
            sourceDiffs = new List<Curve>(tempSourceDiffs);
            // 如果是完全包含的关系，需要处理
            if (tempIntersects.Count == 1 && tempSourceDiffs.Count > 1)
            {
                foreach (var tempSourceDiff in tempSourceDiffs)
                {
                    var duplicate = tempIntersects.FirstOrDefault(it => AreDuplicateEntities(it, tempSourceDiff));
                    if (duplicate != null)
                    {
                        sourceDiffs.Remove(tempSourceDiff);
                        intersects.Remove(duplicate);
                    }
                }
            }
            
            // 获取target polyline和相交部分的差
            var tempTargetDiffs = ClipperBoolean(new List<Curve>() { target }, tempIntersects, ClipType.ctDifference);
            targetDiffs = new List<Curve>(tempTargetDiffs);
            // 如果是完全包含的关系，需要处理
            if (tempIntersects.Count == 1 && tempTargetDiffs.Count > 1)
            {
                foreach (var tempTargetDiff in tempTargetDiffs)
                {
                    var duplicate = tempIntersects.FirstOrDefault(it => AreDuplicateEntities(it, tempTargetDiff));
                    if (duplicate != null)
                    {
                        targetDiffs.Remove(tempTargetDiff);
                        intersects.Remove(duplicate);
                    }
                }
            }
        }

        private static List<Curve> ClipperBoolean(List<Curve> sources, List<Curve> targets, ClipType clipType)
        {
            var booleanResults = new List<Curve>();
            
            var sourceVerticesList = new List<List<Point3d>>();
            var targetVerticesList = new List<List<Point3d>>();
            foreach (var source in sources)
            {
                var sourceVertices = CurveUtils.GetDistinctVertices(source, null);
                sourceVerticesList.Add(sourceVertices);
            }
            
            foreach (var target in targets)
            {
                var targetVertices = CurveUtils.GetDistinctVertices(target, null);
                targetVerticesList.Add(targetVertices);
            }

            var pathes = ClipperBoolean(sourceVerticesList, targetVerticesList, clipType);
            if (pathes.Count <= 0)
                return booleanResults;

            foreach (var path in pathes)
            {
                var polyline = CurveUtils.CreatePolygon(path.ToArray());
                // If the polygon's area is very small, just ignore, or it will bother user.
                if (polyline.Area.Smaller(0.001))
                {
                    polyline.Dispose();
                    continue;
                }
                booleanResults.Add(polyline);
            }
            return booleanResults;
        }

        internal static List<List<Point3d>> ClipperBoolean(List<List<Point3d>> sources, List<List<Point3d>> targets, ClipType clipType)
        {
            var booleanResults = new List<List<Point3d>>();
            // Use clipper to calculate the intersection
            var scale = 100000d;
            var subject = new List<List<IntPoint>>(sources.Count);
            var clipper = new List<List<IntPoint>>(targets.Count);
            var result = new List<List<IntPoint>>();

            var allVertices = new List<Point3d>();
            foreach (var sourceVertices in sources)
            {
                if (sourceVertices[0] == sourceVertices[sourceVertices.Count - 1])
                    sourceVertices.RemoveAt(sourceVertices.Count - 1);
                allVertices.AddRange(sourceVertices);
                var subjectPath = sourceVertices.Select(it => new IntPoint((Int64)(it.X * scale), (Int64)(it.Y * scale))).ToList();
                subject.Add(subjectPath);
            }

            foreach (var targetVertices in targets)
            {
                if (targetVertices[0] == targetVertices[targetVertices.Count - 1])
                    targetVertices.RemoveAt(targetVertices.Count - 1);
                allVertices.AddRange(targetVertices);

                var clipperPath = targetVertices.Select(it => new IntPoint((Int64)(it.X * scale), (Int64)(it.Y * scale))).ToList();
                clipper.Add(clipperPath);
            }

            // 确保有顶点存在，否则没必要计算
            if (allVertices.Count <= 0)
                return booleanResults;

            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            cpr.Execute(clipType, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            if (result.Count <= 0)
            {
                return booleanResults;
            }

            // 进行过bool运算之后，发现有些坐标值都和原坐标偏离了，需要纠正回来，否则会有大量回头线产生。
            var kdTree = new CurveVertexKdTree<Point3d>(allVertices, it => it.ToArray(), ignoreZ: true);
            foreach (var path in result)
            {
                var points = path.Select(it => new Point3d(it.X / scale, it.Y / scale, 0.0)).ToList();
                for (int i = 0; i < points.Count; i++)
                {
                    var nears = kdTree.NearestNeighbours(points[i].ToArray(), 0.01);
                    foreach (var near in nears)
                    {
                        if (near == points[i])
                        {
                            points[i] = near;
                            break;
                        }
                    }
                }
                booleanResults.Add(points);
            }
            return booleanResults;
        }

        private static bool AreDuplicateEntities(Curve source, Curve target)
        {
            if (source == target)
                return false;

            // Check whether all the intersection points are curve's vertices.
            var sourceVertices = CurveUtils.GetDistinctVertices(source, null);
            var targetVertices = CurveUtils.GetDistinctVertices(target, null);
            
            return PolygonIncludeSearcher.AreDuplicateEntities(sourceVertices, targetVertices);
        }

        private struct PolylineVertex
        {
            public PolylineVertex(Point3d point, Curve polyline)
            {
                _point = point;
                _polyline = polyline;
            }

            private readonly Point3d _point;
            public Point3d Point
            {
                get { return _point; }
            }

            private readonly Curve _polyline;
            public Curve Polyline
            {
                get { return _polyline; }
            }

            // Always create an override of Equals for struct
            public override bool Equals(object obj)
            {
                if (obj.GetType() != typeof(PolylineVertex))
                    return false;
                var right = (PolylineVertex)obj;
                var result = Point.IsEqualTo(right.Point) && Polyline == right.Polyline;
                return result;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            // Override operator == and !=
            public static bool operator ==(PolylineVertex left, PolylineVertex right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(PolylineVertex left, PolylineVertex right)
            {
                return !left.Equals(right);
            }
        }

        private static List<Curve> ResolveIncludePolylines(List<Curve> polylines)
        {
            // 用ClipperLib来做布尔运算，会发生一个相互包含的polyline无法截取。
            var includePolylines = CheckInclude(polylines);
            var dictionary = new Dictionary<Curve, List<Curve>>();
            foreach (var pair in includePolylines)
            {
                if (dictionary.ContainsKey(pair.Key))
                {
                    dictionary[pair.Key].Add(pair.Value);
                }
                else
                {
                    dictionary[pair.Key] = new List<Curve>() { pair.Value };
                }
            }

            var replaced = new Dictionary<Curve, List<Curve>>(); 
            foreach (var pair in dictionary)
            {
                var inPolylines = pair.Value;
                // 先将包含的polyline做union，然后再和最外面的polyline做differ.
                if (pair.Value.Count > 1)
                {
                    var sources = new List<Curve>() { pair.Value[0] };
                    pair.Value.RemoveAt(0);
                    var targets = pair.Value;
                    inPolylines = ClipperBoolean(sources, targets, ClipType.ctUnion);
                }

                var differs = ClipperBoolean(new List<Curve>() { pair.Key }, inPolylines, ClipType.ctDifference);
                bool needReplace = true;

                // 如果存在完全包含的清空，要鉴别出来
                if (differs.Count >= 2)
                {
                    foreach (var polyline in differs)
                    {
                        if (AreDuplicateEntities(pair.Key, polyline))
                        {
                            needReplace = false;
                            break;
                        }
                    }
                }
                
                if (needReplace)
                {
                    replaced[pair.Key] = new List<Curve>(differs);
                }
                else
                {
                    foreach (var polyline in differs)
                    {
                        polyline.Dispose();
                    }
                }
                foreach (var inPolyline in inPolylines)
                {
                    if (!pair.Value.Contains(inPolyline))
                        inPolyline.Dispose();
                }
            }

            foreach (var pair in replaced)
            {
                polylines.Remove(pair.Key);
                if (pair.Value.Count > 0)
                {
                    polylines.AddRange(pair.Value);
                }
            }
            return polylines;
        }

        private static IEnumerable<KeyValuePair<Curve, Curve>> CheckInclude(IEnumerable<Curve> polylines)
        {
            var result = new List<KeyValuePair<Curve, Curve>>();
            if (polylines == null || !polylines.Any())
                return result;

            var allVertices = new List<PolylineVertex>();
            foreach (var polyline in polylines)
            {
                var vertices = CurveUtils.GetDistinctVertices(polyline, null);
                allVertices.AddRange(vertices.Select(it => new PolylineVertex(it, polyline)));
            }
            var kdTree = new CurveVertexKdTree<PolylineVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);

            // Use kd tree to check include.
            var analyzed = new HashSet<KeyValuePair<Curve, Curve>>();
            foreach (var polyline in polylines)
            {
                var extents = polyline.GeometricExtents;
                var nearVertices = kdTree.BoxedRange(extents.MinPoint.ToArray(), extents.MaxPoint.ToArray());

                foreach (var curveVertex in nearVertices)
                {
                    if (curveVertex.Polyline == polyline ||
                        analyzed.Contains(new KeyValuePair<Curve, Curve>(polyline, curveVertex.Polyline)))
                    {
                        continue;
                    }

                    analyzed.Add(new KeyValuePair<Curve, Curve>(polyline, curveVertex.Polyline));
                    if (PolygonIncludeSearcher.IsInclude(polyline, curveVertex.Polyline, null))
                    {
                        result.Add(new KeyValuePair<Curve, Curve>(polyline, curveVertex.Polyline));
                    }
                }
            }
            return result;
        }
    }
}
