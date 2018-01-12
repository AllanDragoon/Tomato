using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using QuickGraph;
using QuickGraph.Algorithms.ConnectedComponents;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Algorithms.Search;

namespace LS.MapClean.Addin.Algorithms
{
    public struct CurveVertex
    {
        public CurveVertex(Point3d point, ObjectId objId)
        {
            _point = point;
            _id = objId;
        }

        private Point3d _point;
        public Point3d Point
        {
            get { return _point; }
        }

        private ObjectId _id;
        public ObjectId Id
        {
            get { return _id; }
        }

        // Always create an override of Equals for struct
        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(CurveVertex))
                return false;
            var right = (CurveVertex)obj;
            var result = Point.IsEqualTo(right.Point) && Id == right.Id;
            return result;
        }

        // Override operator == and !=
        public static bool operator ==(CurveVertex left, CurveVertex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CurveVertex left, CurveVertex right)
        {
            return !left.Equals(right);
        }
    }

    public class KdTreeCurveGraphBuilder
    {
        /// <summary>
        /// All ObjectIds which will be used to build the graph.
        /// </summary>
        private IEnumerable<ObjectId> _allIds;
        /// <summary>
        /// AutoCAD database transaction.
        /// </summary>
        private Transaction _transaction;

        // Whether to include self loop.
        private bool _includeSelfLoop = false;

        /// <summary>
        /// Graph which will be built from the input object ids.
        /// </summary>
        private AdjacencyGraph<CurveVertex, SEdge<CurveVertex>> _graph;

        public KdTreeCurveGraphBuilder(IEnumerable<ObjectId> allIds, Transaction transaction, bool includeSelfLoop = false)
        {
            if (allIds == null || !allIds.Any())
                throw new ArgumentNullException("allIds");
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            _allIds = allIds;
            _transaction = transaction;

            _includeSelfLoop = includeSelfLoop;
        }

        public void BuildGraph()
        {
            var visitedIds = new HashSet<ObjectId>();
            var edges = GetEdges(_allIds, visitedIds, _transaction);

            // Build graph
            _graph = edges.ToAdjacencyGraph<CurveVertex, SEdge<CurveVertex>>(allowParallelEdges: true);
        }

        /// <summary>
        /// Search all vertices which are not in a loop.
        ///        /\              /\
        ///       /  \            /  \
        ///      /____\A____C___B/____\
        /// In the above draft, A and B are loops, but C is not.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<CurveVertex> SearchNoneLoopVertices()
        {
            // Find strong connected components
            // https://quickgraph.codeplex.com/wikipage?title=Strongly%20Connected%20Components&referringTitle=User%20Manual

            // A strongly connected component of a graph is a set of vertices such that for each pair u,v of vertices in the component, 
            // there exists a path from u to v and v to u.
            // http://stackoverflow.com/questions/6643076/tarjan-cycle-detection-help-c-sharp

            IDictionary<CurveVertex, int> components = new Dictionary<CurveVertex, int>(); //Key: vertex, Value: subgraph index, 0-based.
            var algorithm = new StronglyConnectedComponentsAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)_graph, components);
            algorithm.Compute();

            var groups = algorithm.Components
                .GroupBy(x => x.Value, x => x.Key)
                .Where(x => x.Count() <= 1);
            var result = new List<CurveVertex>();
            foreach (var group in groups)
            {
                result.AddRange(group);
            }
            return result;
        }

        public IEnumerable<IEnumerable<SEdge<CurveVertex>>> SearchDanglingPaths()
        {
            // Search all none loop vertices
            var noneLoopVertices = SearchNoneLoopVertices();
            var danglingEdges = new List<SEdge<CurveVertex>>();

            foreach (var vertex in noneLoopVertices)
            {
                IEnumerable<SEdge<CurveVertex>> edges = null;
                if (_graph.TryGetOutEdges(vertex, out edges))
                    danglingEdges.AddRange(edges);
            }

            // Create a small graph.
            var tempGraph = danglingEdges.ToAdjacencyGraph<CurveVertex, SEdge<CurveVertex>>();
            // Deep first search the graph and get all its paths.
            var observer = new VertexPredecessorPathRecorderObserver<CurveVertex, SEdge<CurveVertex>>();
            var dfs = new DepthFirstSearchAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)tempGraph);
            using (observer.Attach(dfs))
            {
                dfs.Compute();
            }

            return observer.AllPaths();
        }

        /// <summary>
        /// Search strict dangling vertices, for example
        /// |\
        /// | \
        /// |__\Y_________Z
        /// In the above draft, Z is a dangling vertex, but Y is not.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<CurveVertex> SearchDanglingVertices()
        {
            // Search none loop vertices.
            var noneLoopVertices = SearchNoneLoopVertices();
            
            // Depth first search the graph, and find all the path end vertices.
            var result = new HashSet<CurveVertex>();
            var observer = new VertexPredecessorPathRecorderObserver<CurveVertex, SEdge<CurveVertex>>();
            var dfs = new DepthFirstSearchAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)_graph);
            using (observer.Attach(dfs))
            {
                dfs.Compute();
            }
            // Dangling vertex must be an end path vertex.
            foreach (var curveVertex in observer.EndPathVertices)
            {
                if (!noneLoopVertices.Contains(curveVertex))
                    continue;

                result.Add(curveVertex);
                // Check whether this path's root point is a dangling vertex.
                var root = GetRootVertex(observer.VertexPredecessors, curveVertex);
                if (root != null && noneLoopVertices.Contains(root.Value))
                    result.Add(root.Value);
            }
            return result;
        }

        CurveVertex? GetRootVertex(IDictionary<CurveVertex, SEdge<CurveVertex>> predecessors, CurveVertex vertex)
        {
            CurveVertex? target = vertex;
            CurveVertex? source = null;
            
            while (target != null)
            {
                if (predecessors.ContainsKey(target.Value))
                {
                    source = predecessors[target.Value].Source;
                    target = source;
                }
                else
                {
                    target = null;
                }
            }
            return source;
        }

        #region Utils

        private void BuildCurveVertexKdTree(IEnumerable<ObjectId> allIds, Transaction transaction,
            out Dictionary<ObjectId, CurveVertex[]> curveVertices, out CurveVertexKdTree<CurveVertex> kdTree)
        {
            curveVertices = new Dictionary<ObjectId, CurveVertex[]>();
            kdTree = null;
            var allVertices = new List<CurveVertex>();
            foreach (var id in allIds)
            {
                var points = CurveUtils.GetCurveEndPoints(id, transaction);
                if (points.Length <= 0)
                    continue;

                var vertices = points.Select(it => new CurveVertex(it, id));
                curveVertices[id] = vertices.ToArray();

                allVertices.AddRange(vertices);
            }

            kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it=>it.Point, ignoreZ:true);
        }

        private IEnumerable<SEdge<CurveVertex>> GetEdges(IEnumerable<ObjectId> allIds, HashSet<ObjectId> visitedIds, Transaction transaction)
        {
            Dictionary<ObjectId, CurveVertex[]> curveVertices = null;
            CurveVertexKdTree<CurveVertex> kdTree = null;
            BuildCurveVertexKdTree(allIds, transaction, out curveVertices, out kdTree);

            var result = new List<SEdge<CurveVertex>>();
            var candidates = new Stack<KeyValuePair<ObjectId, CurveVertex>>();

            var pendingIds = allIds.Except(visitedIds).ToList();
            while (pendingIds.Count > 0)
            {
                // 1. Arbitrary select a curve as the started curve
                foreach (ObjectId objId in pendingIds)
                {
                    var edges = VisitCurve(null, objId, pendingIds, visitedIds, candidates, curveVertices, kdTree, transaction);
                    if (edges.Any())
                    {
                        result.AddRange(edges);
                        break;
                    }
                }

                // 2. Start popup the candidates and dispose them
                while (candidates.Count > 0)
                {
                    var pair = candidates.Pop();
                    // There is the case that it has been visited, such as the last line in a loop.
                    if (visitedIds.Contains(pair.Key))
                        continue;

                    var edges = VisitCurve(pair.Value, pair.Key, pendingIds, visitedIds, candidates, curveVertices, kdTree, transaction);
                    result.AddRange(edges);
                }

                // Shrink the pending ids collection.
                pendingIds = pendingIds.Except(visitedIds).ToList();
            }

            return result;
        }

        private IEnumerable<SEdge<CurveVertex>> VisitCurve(CurveVertex? sourceVertex, ObjectId id, IEnumerable<ObjectId> allIds,
            HashSet<ObjectId> visitedIds, Stack<KeyValuePair<ObjectId, CurveVertex>> candidates, 
            Dictionary<ObjectId, CurveVertex[]> curveVertices, CurveVertexKdTree<CurveVertex> kdTree,
            Transaction transaction)
        {
            // No need to handle it.
            if (!curveVertices.ContainsKey(id))
            {
                visitedIds.Add(id);
                return new SEdge<CurveVertex>[0];
            }

            var originVertices = curveVertices[id];
            var points = originVertices.Select(it => it.Point).ToArray();
            // If none of the curve's end points is equal to the point, just return.
            // It means this curve is not a qualified one because it's not connected to sourceVertex.
            // Then it will be handled later because it belongs to another loop.
            if (sourceVertex != null && points.Length > 0 && !points.Contains(sourceVertex.Value.Point))
            {
                bool contains = false;
                foreach (var point in points)
                {
                    if (point.IsEqualTo(sourceVertex.Value.Point))
                        contains = true;
                }

                if (!contains)
                    return new SEdge<CurveVertex>[0];
            }

            // Mark it as visited.
            visitedIds.Add(id);
            // If there is no end points on the entity, just return.
            if (originVertices.Length <= 0)
            {
                return new SEdge<CurveVertex>[0];
            }

            // Remove the duplicate point
            // Note: if a curve has 2 same end points, means its a self-loop.
            var distinctVertices = new List<CurveVertex>();
            CurveVertex current = originVertices[0];
            distinctVertices.Add(current);
            for (int i = 1; i < originVertices.Length; i++)
            {
                if (originVertices[i] != current)
                {
                    current = originVertices[i];
                    distinctVertices.Add(current);
                }
            }

            var result = new List<SEdge<CurveVertex>>();
            // Push vertex and adjacent curve into candidates
            foreach (var vertex in distinctVertices)
            {
                if (sourceVertex != null && sourceVertex.Value.Point.IsEqualTo(vertex.Point))
                    continue;

                var adjacentVertices = kdTree.NearestNeighbours(vertex.Point, radius: 0.1);
                var adjacentIds = adjacentVertices.Select(it => it.Id);
                foreach (ObjectId adjacentId in adjacentIds)
                {
                    // If it has been visited.
                    if (visitedIds.Contains(adjacentId))
                    {
                        var originVertex = new CurveVertex(vertex.Point, adjacentId);
                        // If this curve has been pushed into the candidates stack before, there must exist a loop.
                        // So we create a back edge to the original vertex.
                        // NOTE: Use candidates.Contains(new KeyValuePair<ObjectId, CurveVertex>(id, originVertex))
                        // will cause a problem if two point are very close to each other
                        // such as (10.5987284839403,10.2186528623464,0) and (10.5987284839408,10.2186528623464,0)
                        var existing = candidates.FirstOrDefault(it => it.Key == id && it.Value == originVertex);
                        if (!existing.Equals(default(KeyValuePair<ObjectId, CurveVertex>)))
                        {
                            // Create edge back to the old vertex.
                            result.Add(new SEdge<CurveVertex>(vertex, existing.Value));
                        }
                        //if (candidates.Contains(new KeyValuePair<ObjectId, CurveVertex>(id, originVertex)))
                        //{
                        //    // Create edge back to the old vertex.
                        //    result.Add(new SEdge<CurveVertex>(vertex, originVertex));
                        //}
                        continue;
                    }

                    // If it's not in the collection of selected ids.
                    if (!allIds.Contains(adjacentId))
                        continue;

                    candidates.Push(new KeyValuePair<ObjectId, CurveVertex>(adjacentId, vertex));
                }
            }

            // Create edges
            int start = 0;
            if (sourceVertex != null)
            {
                for (int i = 0; i < distinctVertices.Count; i++)
                {
                    if (distinctVertices[i].Point.IsEqualTo(sourceVertex.Value.Point))
                    {
                        start = i;
                        // Add edge from sourceVertex to nearest point.
                        result.Add(new SEdge<CurveVertex>(sourceVertex.Value, distinctVertices[start]));
                        break;
                    }
                }
            }

            if (distinctVertices.Count == 1)
            {
                // Allan 2015/05/06: Self loop won't be handled - to improve performance.
                
                // Self loop, create dummy vertex to let it to be a real loop
                if (_includeSelfLoop)
                {
                    var dummy = new CurveVertex(distinctVertices[0].Point + new Vector3d(1, 1, 0), id);
                    result.Add(new SEdge<CurveVertex>(distinctVertices[0], dummy));
                    result.Add(new SEdge<CurveVertex>(dummy, distinctVertices[0]));
                }
            }
            else
            {
                // Must start from the vertex whose poitn is equal to source vertex's point.
                // Up
                var previousVertex = distinctVertices[start];
                for (int i = start + 1; i < distinctVertices.Count; i++)
                {
                    // Create a middle point to fix a bug - to avoid a line's two vertices are both in loop,
                    // but actually the line is not in a loop, for example
                    //        /\             /\
                    //       /  \           /  \
                    //      /____\A_______B/____\
                    // Point A and point B are both in a loop, but line AB is not in a loop.
                    // If I add a dummy point C in the middle of AB, then our graph could easily
                    // to determine C is not in a loop.
                    var middlePoint = previousVertex.Point + (distinctVertices[i].Point - previousVertex.Point) / 2.0;
                    var middleVertex = new CurveVertex(middlePoint, id);
                    result.Add(new SEdge<CurveVertex>(previousVertex, middleVertex));
                    result.Add(new SEdge<CurveVertex>(middleVertex, distinctVertices[i]));
                    previousVertex = distinctVertices[i];
                }
                // Down
                previousVertex = distinctVertices[start];
                for (int i = start - 1; i >= 0; i--)
                {
                    var middlePoint = previousVertex.Point + (distinctVertices[i].Point - previousVertex.Point) / 2.0;
                    var middleVertex = new CurveVertex(middlePoint, id);
                    result.Add(new SEdge<CurveVertex>(previousVertex, middleVertex));
                    result.Add(new SEdge<CurveVertex>(middleVertex, distinctVertices[i]));

                    previousVertex = distinctVertices[i];
                }
            }

            return result;
        }

        #endregion

        #region Search All Loops
        private VertexPredecessorRecorderObserver<CurveVertex, SEdge<CurveVertex>>  _observer;
        private List<SEdge<CurveVertex>[]> _allLoops = new List<SEdge<CurveVertex>[]>();
        public IEnumerable<SEdge<CurveVertex>[]> SearchLoops()
        {
            _allLoops.Clear();

            // create algorithm
            var dfs = new DepthFirstSearchAlgorithm<CurveVertex, SEdge<CurveVertex>>(_graph);
            _observer = new VertexPredecessorRecorderObserver<CurveVertex, SEdge<CurveVertex>>();
            using (var attacher = _observer.Attach(dfs))
            {
                dfs.BackEdge += OnDfsBackEdge;
                dfs.ForwardOrCrossEdge += OnDfsForwardOrCrossEdge;
                //do the search
                dfs.Compute();
                return _allLoops;
            }
        }

        public IEnumerable<ObjectId> SearchAllIdsInLoop()
        {
            var loops = SearchLoops();
            var ids = new HashSet<ObjectId>();
            foreach (var loop in loops)
            {
                foreach (var sEdge in loop)
                {
                    var sourceId = sEdge.Source.Id;
                    var targetId = sEdge.Target.Id;
                    // Only when sourceId is equal to targetId
                    if (sourceId != targetId)
                        continue;
                    ids.Add(sourceId);
                }
            }
            return ids;
        }

        private void OnDfsForwardOrCrossEdge(SEdge<CurveVertex> e)
        {
            
        }

        private void OnDfsBackEdge(SEdge<CurveVertex> e)
        {
            // e.Target --> e.Source is a back edge, that means there is a loop.
            var loop = new List<SEdge<CurveVertex>>();
            loop.Add(e);
            var edge = e;
            do
            {
                var prevEdge = _observer.VertexPredecessors[edge.Source];
                loop.Add(prevEdge);
                if (prevEdge.Source == e.Target)
                    break;
                edge = prevEdge;
            } while (true);
            _allLoops.Add(loop.ToArray());
        }
        #endregion
    }

    [Obsolete]
    class CurveGraphBuilder
    {
        /// <summary>
        /// All ObjectIds which will be used to build the graph.
        /// </summary>
        private IEnumerable<ObjectId> _allIds;
        /// <summary>
        /// AutoCAD database transaction.
        /// </summary>
        private Transaction _transaction;

        /// <summary>
        /// Graph which will be built from the input object ids.
        /// </summary>
        private AdjacencyGraph<CurveVertex, SEdge<CurveVertex>> _graph;

        /// <summary>
        /// Delegate of select curves at a point.
        /// </summary>
        public Func<Point3d, ObjectId[]> SelectCurves { get; set; }

        public CurveGraphBuilder(IEnumerable<ObjectId> allIds, Transaction transaction)
        {
            if (allIds == null || !allIds.Any())
                throw new ArgumentNullException("allIds");
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            _allIds = allIds;
            _transaction = transaction;
        }

        public void BuildGraph()
        {
            var visitedIds = new HashSet<ObjectId>();
            var edges = GetEdges(_allIds, visitedIds, _transaction);

            // Build graph
            _graph = edges.ToAdjacencyGraph<CurveVertex, SEdge<CurveVertex>>(allowParallelEdges: true);
        }

        /// <summary>
        /// Search all vertices which are not in a loop.
        ///        /\              /\
        ///       /  \            /  \
        ///      /____\A____C___B/____\
        /// In the above draft, A and B are loops, but C is not.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<CurveVertex> SearchNoneLoopVertices()
        {
            // Find strong connected components
            // https://quickgraph.codeplex.com/wikipage?title=Strongly%20Connected%20Components&referringTitle=User%20Manual

            // A strongly connected component of a graph is a set of vertices such that for each pair u,v of vertices in the component, 
            // there exists a path from u to v and v to u.
            // http://stackoverflow.com/questions/6643076/tarjan-cycle-detection-help-c-sharp

            IDictionary<CurveVertex, int> components = new Dictionary<CurveVertex, int>(); //Key: vertex, Value: subgraph index, 0-based.
            var algorithm = new StronglyConnectedComponentsAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)_graph, components);
            algorithm.Compute();

            var groups = algorithm.Components
                .GroupBy(x => x.Value, x => x.Key)
                .Where(x => x.Count() <= 1);
            var result = new List<CurveVertex>();
            foreach (var group in groups)
            {
                result.AddRange(group);
            }
            return result;
        }

        public IEnumerable<IEnumerable<SEdge<CurveVertex>>> SearchDanglingPaths()
        {
            // Search all none loop vertices
            var noneLoopVertices = SearchNoneLoopVertices();
            var danglingEdges = new List<SEdge<CurveVertex>>();

            foreach (var vertex in noneLoopVertices)
            {
                IEnumerable<SEdge<CurveVertex>> edges = null;
                if (_graph.TryGetOutEdges(vertex, out edges))
                    danglingEdges.AddRange(edges);
            }

            // Create a small graph.
            var tempGraph = danglingEdges.ToAdjacencyGraph<CurveVertex, SEdge<CurveVertex>>();
            // Deep first search the graph and get all its paths.
            var observer = new VertexPredecessorPathRecorderObserver<CurveVertex, SEdge<CurveVertex>>();
            var dfs = new DepthFirstSearchAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)tempGraph);
            using (observer.Attach(dfs))
            {
                dfs.Compute();
            }

            return observer.AllPaths();
        }

        /// <summary>
        /// Search strict dangling vertices, for example
        /// |\
        /// | \
        /// |__\Y_________Z
        /// In the above draft, Z is a dangling vertex, but Y is not.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<CurveVertex> SearchDanglingVertices()
        {
            // Search none loop vertices.
            var noneLoopVertices = SearchNoneLoopVertices();

            // Depth first search the graph, and find all the path end vertices.
            var result = new HashSet<CurveVertex>();
            var observer = new VertexPredecessorPathRecorderObserver<CurveVertex, SEdge<CurveVertex>>();
            var dfs = new DepthFirstSearchAlgorithm<CurveVertex, SEdge<CurveVertex>>((IVertexListGraph<CurveVertex, SEdge<CurveVertex>>)_graph);
            using (observer.Attach(dfs))
            {
                dfs.Compute();
            }
            // Dangling vertex must be an end path vertex.
            foreach (var curveVertex in observer.EndPathVertices)
            {
                if (!noneLoopVertices.Contains(curveVertex))
                    continue;

                result.Add(curveVertex);
                // Check whether this path's root point is a dangling vertex.
                var root = GetRootVertex(observer.VertexPredecessors, curveVertex);
                if (root != null && noneLoopVertices.Contains(root.Value))
                    result.Add(root.Value);
            }
            return result;
        }

        CurveVertex? GetRootVertex(IDictionary<CurveVertex, SEdge<CurveVertex>> predecessors, CurveVertex vertex)
        {
            CurveVertex? target = vertex;
            CurveVertex? source = null;

            while (target != null)
            {
                if (predecessors.ContainsKey(target.Value))
                {
                    source = predecessors[target.Value].Source;
                    target = source;
                }
                else
                {
                    target = null;
                }
            }
            return source;
        }

        #region Utils
        private IEnumerable<SEdge<CurveVertex>> GetEdges(IEnumerable<ObjectId> allIds, HashSet<ObjectId> visitedIds, Transaction transaction)
        {
            var result = new List<SEdge<CurveVertex>>();
            var candidates = new Stack<KeyValuePair<ObjectId, CurveVertex>>();

            var pendingIds = allIds.Except(visitedIds).ToList();
            while (pendingIds.Count > 0)
            {
                // 1. Arbitrary select a curve as the started curve
                foreach (ObjectId objId in pendingIds)
                {
                    var edges = VisitCurve(null, objId, pendingIds, visitedIds, candidates, transaction);
                    if (edges.Any())
                    {
                        result.AddRange(edges);
                        break;
                    }
                }

                // 2. Start popup the candidates and dispose them
                while (candidates.Count > 0)
                {
                    var pair = candidates.Pop();
                    // There is the case that it has been visited, such as the last line in a loop.
                    if (visitedIds.Contains(pair.Key))
                        continue;

                    var edges = VisitCurve(pair.Value, pair.Key, pendingIds, visitedIds, candidates, transaction);
                    result.AddRange(edges);
                }

                // Shrink the pending ids collection.
                pendingIds = pendingIds.Except(visitedIds).ToList();
            }

            return result;
        }

        private IEnumerable<SEdge<CurveVertex>> VisitCurve(CurveVertex? sourceVertex, ObjectId id, IEnumerable<ObjectId> allIds,
            HashSet<ObjectId> visitedIds, Stack<KeyValuePair<ObjectId, CurveVertex>> candidates, Transaction transaction)
        {
            var points = CurveUtils.GetCurveEndPoints(id, transaction);
            // If none of the curve's end points is equal to the point, just return.
            // It means this curve is not a qualified one because it's not connected to sourceVertex.
            // Then it will be handled later because it belongs to another loop.
            if (sourceVertex != null && points.Length > 0 && !points.Contains(sourceVertex.Value.Point))
            {
                bool contains = false;
                foreach (var point in points)
                {
                    if (point.IsEqualTo(sourceVertex.Value.Point))
                        contains = true;
                }

                if (!contains)
                    return new SEdge<CurveVertex>[0];
            }

            // Mark it as visited.
            visitedIds.Add(id);
            // If there is no end points on the entity, just return.
            if (points.Length <= 0)
            {
                return new SEdge<CurveVertex>[0];
            }

            // Remove the duplicate point
            // Note: if a curve has 2 same end points, means its a self-loop.
            var distinctPoints = new List<Point3d>();
            Point3d current = points[0];
            distinctPoints.Add(current);
            for (int i = 1; i < points.Length; i++)
            {
                if (points[i] != current)
                {
                    current = points[i];
                    distinctPoints.Add(current);
                }
            }

            // Create vertices for the curve
            var vertices = new CurveVertex[distinctPoints.Count];
            for (int i = 0; i < distinctPoints.Count; i++)
            {
                vertices[i] = new CurveVertex(distinctPoints[i], id);
            }

            var result = new List<SEdge<CurveVertex>>();
            // Push vertex and adjacent curve into candidates
            foreach (var vertex in vertices)
            {
                if (sourceVertex != null && sourceVertex.Value.Point.IsEqualTo(vertex.Point))
                    continue;

                var adjacentIds = SelectCurves(vertex.Point);
                foreach (ObjectId adjacentId in adjacentIds)
                {
                    // If it has been visited.
                    if (visitedIds.Contains(adjacentId))
                    {
                        var originVertex = new CurveVertex(vertex.Point, adjacentId);
                        // If this curve has been pushed into the candidates stack before, there must exist a loop.
                        // So we create a back edge to the original vertex.
                        // NOTE: Use candidates.Contains(new KeyValuePair<ObjectId, CurveVertex>(id, originVertex))
                        // will cause a problem if two point are very close to each other
                        // such as (10.5987284839403,10.2186528623464,0) and (10.5987284839408,10.2186528623464,0)
                        var existing = candidates.FirstOrDefault(it => it.Key == id && it.Value == originVertex);
                        if (!existing.Equals(default(KeyValuePair<ObjectId, CurveVertex>)))
                        {
                            // Create edge back to the old vertex.
                            result.Add(new SEdge<CurveVertex>(vertex, existing.Value));
                        }
                        //if (candidates.Contains(new KeyValuePair<ObjectId, CurveVertex>(id, originVertex)))
                        //{
                        //    // Create edge back to the old vertex.
                        //    result.Add(new SEdge<CurveVertex>(vertex, originVertex));
                        //}
                        continue;
                    }

                    // If it's not in the collection of selected ids.
                    if (!allIds.Contains(adjacentId))
                        continue;

                    candidates.Push(new KeyValuePair<ObjectId, CurveVertex>(adjacentId, vertex));
                }
            }

            // Create edges
            int start = 0;
            if (sourceVertex != null)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].Point.IsEqualTo(sourceVertex.Value.Point))
                    {
                        start = i;
                        // Add edge from sourceVertex to nearest point.
                        result.Add(new SEdge<CurveVertex>(sourceVertex.Value, vertices[start]));
                        break;
                    }
                }
            }

            if (vertices.Length == 1)
            {
                // Allan 2015/05/06: Self loop won't be handled - to improve performance.

                //// Self loop, create dummy vertex to let it to be a real loop
                //var dummy = new CurveVertex(vertices[0].Point + new Vector3d(1, 1, 1), id);
                //result.Add(new SEdge<CurveVertex>(vertices[0], dummy));
                //result.Add(new SEdge<CurveVertex>(dummy, vertices[0]));
            }
            else
            {
                // Must start from the vertex whose poitn is equal to source vertex's point.
                // Up
                var previousVertex = vertices[start];
                for (int i = start + 1; i < vertices.Length; i++)
                {
                    // Create a middle point to fix a bug - to avoid a line's two vertices are both in loop,
                    // but actually the line is not in a loop, for example
                    //        /\             /\
                    //       /  \           /  \
                    //      /____\A_______B/____\
                    // Point A and point B are both in a loop, but line AB is not in a loop.
                    // If I add a dummy point C in the middle of AB, then our graph could easily
                    // to determine C is not in a loop.
                    var middlePoint = previousVertex.Point + (vertices[i].Point - previousVertex.Point) / 2.0;
                    var middleVertex = new CurveVertex(middlePoint, id);
                    result.Add(new SEdge<CurveVertex>(previousVertex, middleVertex));
                    result.Add(new SEdge<CurveVertex>(middleVertex, vertices[i]));
                    previousVertex = vertices[i];
                }
                // Down
                previousVertex = vertices[start];
                for (int i = start - 1; i >= 0; i--)
                {
                    var middlePoint = previousVertex.Point + (vertices[i].Point - previousVertex.Point) / 2.0;
                    var middleVertex = new CurveVertex(middlePoint, id);
                    result.Add(new SEdge<CurveVertex>(previousVertex, middleVertex));
                    result.Add(new SEdge<CurveVertex>(middleVertex, vertices[i]));

                    previousVertex = vertices[i];
                }
            }

            return result;
        }

        #endregion
    }
}
