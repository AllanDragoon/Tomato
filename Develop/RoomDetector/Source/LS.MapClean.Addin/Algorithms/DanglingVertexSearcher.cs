using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// Search all dangling curve in the drawing.
    /// </summary>
    class DanglingVertexSearcher
    {
        private IEnumerable<ObjectId> _allIds;
        private Transaction _transaction;

        // If true, no need to check whether an id is in _allIds, used to improve performance.
        private bool _forDrawing;

        /// <summary>
        /// Delegate of select curves at a point.
        /// Must be set before calling Search method.
        /// </summary>
        public Func<Point3d, ObjectId[]> SelectCurves { get; set; }

        public DanglingVertexSearcher(IEnumerable<ObjectId> allIds, bool forDrawing, Transaction transaction)
        {
            _allIds = allIds;
            _forDrawing = forDrawing;
            _transaction = transaction;
        }

        public IEnumerable<CurveVertex> Search()
        {
            var result = new List<CurveVertex>();
            var visitedIds = new HashSet<ObjectId>();
            var candidates = new Stack<CurveVertex>();

            var pendingIds = _allIds.Except(visitedIds).ToList();
            while (pendingIds.Count > 0)
            {
                // 1. Arbitrary select a curve as the started curve
                foreach (ObjectId objId in pendingIds)
                {
                    // Mark it as visited.
                    visitedIds.Add(objId);
                    // if it has vertex, break.
                    var vertices = GetCurveVertices(objId, visitedIds);
                    if (vertices.Any())
                    {
                        foreach (var curveVertex in vertices)
                        {
                            candidates.Push(curveVertex);
                        }
                        break;
                    }
                }

                // 2. Start popup the candidates and dispose them
                while (candidates.Count > 0)
                {
                    var vertex = candidates.Pop();
                    var isDangling = CheckDangling(vertex, pendingIds, visitedIds, candidates);
                    if (isDangling)
                    {
                        result.Add(vertex);
                    }
                }

                // Shrink the pending ids collection.
                pendingIds = pendingIds.Except(visitedIds).ToList();
            }
            return result;
        }

        private IEnumerable<CurveVertex> GetCurveVertices(ObjectId id, HashSet<ObjectId> visitedIds)
        {
            var points = CurveUtils.GetCurveEndPoints(id, _transaction);
            if (points.Length <= 0)
            {
                return new CurveVertex[0];
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
            if (distinctPoints.Count <= 1)
                return new CurveVertex[0];

            // Create vertices for the curve
            var vertices = new CurveVertex[distinctPoints.Count];
            for (int i = 0; i < distinctPoints.Count; i++)
            {
                vertices[i] = new CurveVertex(distinctPoints[i], id);
            }
            return vertices;
        }

        private bool CheckDangling(CurveVertex vertex, IEnumerable<ObjectId> allIds, HashSet<ObjectId> visitedIds, Stack<CurveVertex> candidates)
        {
            bool isDangling = true;

            // Get entities which vertex is on.
            var adjacentIds = SelectCurves(vertex.Point);
            foreach (ObjectId adjacentId in adjacentIds)
            {
                if (adjacentId == vertex.Id)
                    continue;
                
                // Check adjacent vertices
                var vertices = GetCurveVertices(adjacentId, visitedIds);

                foreach (var adjVertex in vertices)
                {
                    // If it connect to vertex, isDangling is false.
                    if (adjVertex.Point == vertex.Point)
                    {
                        isDangling = false;
                    }
                    else if (!visitedIds.Contains(adjacentId) && 
                        (_forDrawing || allIds.Contains(adjacentId)))
                    {
                        candidates.Push(adjVertex);
                    }
                }

                // Mark it as visited.
                visitedIds.Add(adjacentId);
            }
            return isDangling;
        }
    }

    /// <summary>
    /// Search all dangling curve in the drawing by KdTree.
    /// </summary>
    class KdTreeDanglingVertexSearcher
    {
        private IEnumerable<ObjectId> _allIds;
        private Transaction _transaction;

        // If true, no need to check whether an id is in _allIds, used to improve performance.
        private bool _forDrawing;

        public KdTreeDanglingVertexSearcher(IEnumerable<ObjectId> allIds, bool forDrawing, Transaction transaction)
        {
            _allIds = allIds;
            _forDrawing = forDrawing;
            _transaction = transaction;
        }

        public IEnumerable<CurveVertex> Search()
        {
            var result = new List<CurveVertex>();
            var visitedIds = new HashSet<ObjectId>();
            var candidates = new Stack<CurveVertex>();

            Dictionary<ObjectId, CurveVertex[]> curveVertices = null;
            CurveVertexKdTree<CurveVertex> kdTree = null;
            BuildCurveVertexKdTree(_allIds, _transaction, out curveVertices, out kdTree);

            var pendingIds = _allIds.Except(visitedIds).ToList();
            while (pendingIds.Count > 0)
            {
                // 1. Arbitrary select a curve as the started curve
                foreach (ObjectId objId in pendingIds)
                {
                    // Mark it as visited.
                    visitedIds.Add(objId);
                    // if it has vertex, break.
                    if (curveVertices.ContainsKey(objId))
                    {
                        foreach (var curveVertex in curveVertices[objId])
                        {
                            candidates.Push(curveVertex);
                        }
                        break;
                    }
                }

                // 2. Start popup the candidates and dispose them
                while (candidates.Count > 0)
                {
                    var vertex = candidates.Pop();
                    var isDangling = CheckDangling(vertex, pendingIds, visitedIds, candidates, curveVertices, kdTree);
                    if (isDangling)
                    {
                        result.Add(vertex);
                    }
                }

                // Shrink the pending ids collection.
                pendingIds = pendingIds.Except(visitedIds).ToList();
            }
            return result;
        }

        private void BuildCurveVertexKdTree(IEnumerable<ObjectId> allIds, Transaction transaction,
            out Dictionary<ObjectId, CurveVertex[]> curveVertices, out CurveVertexKdTree<CurveVertex> kdTree)
        {
            kdTree = null;
            curveVertices = new Dictionary<ObjectId, CurveVertex[]>();
            var allVertices = new List<CurveVertex>();
            foreach (var id in allIds)
            {
                var points = CurveUtils.GetCurveEndPoints(id, transaction);
                if (points.Length <= 0)
                    continue;

                var distinctPoints = GetDistinctPoints(points);
                // If distinctPoints.Length == 1, means it's self-loop
                if (distinctPoints.Length <= 1)
                    continue;

                var vertices = distinctPoints.Select(it => new CurveVertex(it, id));
                curveVertices[id] = vertices.ToArray();
                allVertices.AddRange(vertices);
            }
            kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it=>it.Point, ignoreZ: true);
        }

        private Point3d[] GetDistinctPoints(Point3d[] points)
        {
            var distinctPoints = new List<Point3d>();
            Point3d current = points[0];
            distinctPoints.Add(current);
            for (int i = 1; i < points.Length; i++)
            {
                if (!points[i].IsEqualTo(current))
                {
                    current = points[i];
                    distinctPoints.Add(current);
                }
            }
            return distinctPoints.ToArray();
        }

        private bool CheckDangling(CurveVertex vertex, IEnumerable<ObjectId> allIds,
            HashSet<ObjectId> visitedIds, Stack<CurveVertex> candidates,
            Dictionary<ObjectId, CurveVertex[]> curveVertices, CurveVertexKdTree<CurveVertex> kdTree)
        {
            bool isDangling = true;

            // Get entities which vertex is on.
            var neighbors = kdTree.NearestNeighbours(vertex.Point, radius: 0.1);
            var adjacentIds = neighbors.Select(it => it.Id);

            foreach (ObjectId adjacentId in adjacentIds)
            {
                if (adjacentId == vertex.Id)
                    continue;

                // Check adjacent vertices
                if (!curveVertices.ContainsKey(adjacentId))
                    continue;

                var vertices = curveVertices[adjacentId];
                foreach (var adjVertex in vertices)
                {
                    // If it connect to vertex, isDangling is false.
                    if (adjVertex.Point == vertex.Point)
                    {
                        isDangling = false;
                    }
                    else if (!visitedIds.Contains(adjacentId) &&
                        (_forDrawing || allIds.Contains(adjacentId)))
                    {
                        candidates.Push(adjVertex);
                    }
                }

                // Mark it as visited.
                visitedIds.Add(adjacentId);
            }
            return isDangling;
        }
    }
}
