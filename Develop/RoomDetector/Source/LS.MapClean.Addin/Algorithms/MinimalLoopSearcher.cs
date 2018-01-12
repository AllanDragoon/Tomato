using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using QuickGraph;

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
    }
}
