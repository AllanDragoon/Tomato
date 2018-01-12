using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows.Data;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// Node of a Point3dTree
    /// </summary>
    public class CurveVertexNode<T>
    {
        /// <summary>
        /// Creates a new instance of TreeNode
        /// </summary>
        /// <param name="value">The 3d point value of the node.</param>
        public CurveVertexNode(T value) { this.Value = value; }

        /// <summary>
        /// Gets the value (CurveVertex) of the node.
        /// </summary>
        public T Value { get; internal set; }

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        public CurveVertexNode<T> Parent { get; internal set; }

        /// <summary>
        /// Gets the left child node.
        /// </summary>
        public CurveVertexNode<T> LeftChild { get; internal set; }

        /// <summary>
        /// Gets the right child node.
        /// </summary>
        public CurveVertexNode<T> RightChild { get; internal set; }

        /// <summary>
        /// Gets the depth of the node in tree.
        /// </summary>
        public int Depth { get; internal set; }

        /// <summary>
        /// Gets a value indicating if the current node is a LeftChild node.
        /// </summary>
        public bool IsLeft { get; internal set; }
    }

    /// <summary>
    /// Provides methods to organize 3d points in a Kd tree structure to speed up the search of neighbours.
    /// A boolean constructor parameter (ignoreZ) indicates if the resulting Kd tree is a 3d tree or a 2d tree.
    /// Use ignoreZ = true if all points in the input collection lie on a plane parallel to XY
    /// or if the points have to be considered as projected on the XY plane.
    /// http://www.acadnetwork.com/index.php?topic=352.0
    /// </summary>
    public class CurveVertexKdTree<T>
    {
        #region Private fields

        private int _dimension;
        private int _parallelDepth;
        private bool _ignoreZ;
        private Func<Point3d, Point3d, double> _sqrDist;
        private Func<T, Point3d> _pointSelector; 
        #endregion

        #region Constructor

        /// <summary>
        /// Creates an new instance of Point3dTree.
        /// </summary>
        /// <param name="vertices">The Point3d collection to fill the tree.</param>
        /// <param name="ignoreZ">A value indicating if the Z coordinate of points is ignored
        /// (as if all points were projected to the XY plane).</param>
        public CurveVertexKdTree(IEnumerable<T> vertices, Func<T,Point3d> pointSelector, bool ignoreZ = false)
        {
            if (vertices == null)
                throw new ArgumentNullException("vertices");
            if (pointSelector == null)
                throw new ArgumentNullException("pointSelector");

            this._ignoreZ = ignoreZ;
            this._dimension = ignoreZ ? 2 : 3;
            this._pointSelector = pointSelector;

            if (ignoreZ)
                this._sqrDist = SqrDistance2d;
            else
                this._sqrDist = SqrDistance3d;
            int numProc = System.Environment.ProcessorCount;
            this._parallelDepth = -1;
            while (numProc >> ++this._parallelDepth > 1) ;
            var distinctVertices = vertices.Distinct().ToArray();
            this.Root = Create(distinctVertices, 0, null, false);
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Gets the root node of the tree.
        /// </summary>
        public CurveVertexNode<T> Root { get; private set; }

        #endregion

        #region Public methods

        /// <summary>
        /// Gets the nearest neighbour.
        /// </summary>
        /// <param name="point">The point from which search the nearest neighbour.</param>
        /// <returns>The nearest point in the collection from the specified one.</returns>
        public T NearestNeighbour(Point3d point)
        {
            return GetNeighbour(point, this.Root, this.Root.Value, double.MaxValue);
        }

        /// <summary>
        /// Gets the neighbours within the specified distance.
        /// </summary>
        /// <param name="point">The point from which search the nearest neighbours.</param>
        /// <param name="radius">The distance in which collect the neighbours.</param>
        /// <returns>The points which distance from the specified point is less or equal to the specified distance.</returns>
        public IEnumerable<T> NearestNeighbours(Point3d point, double radius)
        {
            var vertices = new List<T>();
            GetNeighboursAtDistance(point, radius * radius, this.Root, vertices);
            return vertices;
        }

        /// <summary>
        /// Gets the given number of nearest neighbours.
        /// </summary>
        /// <param name="point">The point from which search the nearest neighbours.</param>
        /// <param name="number">The number of points to collect.</param>
        /// <returns>The n nearest neighbours of the specified point.</returns>
        public IEnumerable<T> NearestNeighbours(Point3d point, int number)
        {
            List<Tuple<double, T>> pairs = new List<Tuple<double, T>>(number);
            GetKNeighbours(point, number, this.Root, pairs);
            var vertices = new List<T>();
            for (int i = 0; i < pairs.Count; i++)
            {
                vertices.Add(pairs[i].Item2);
            }
            return vertices;
        }

        /// <summary>
        /// Gets the points in a range.
        /// </summary>
        /// <param name="pt1">The first corner of range.</param>
        /// <param name="pt2">The opposite corner of the range.</param>
        /// <returns>All points within the box.</returns>
        public IEnumerable<T> BoxedRange(Point3d pt1, Point3d pt2)
        {
            Point3d lowerLeft = new Point3d(
                Math.Min(pt1.X, pt2.X), Math.Min(pt1.Y, pt2.Y), Math.Min(pt1.Z, pt2.Z));
            Point3d upperRight = new Point3d(
                Math.Max(pt1.X, pt2.X), Math.Max(pt1.Y, pt2.Y), Math.Max(pt1.Z, pt2.Z));
            var vertices = new List<T>();
            FindRange(lowerLeft, upperRight, this.Root, vertices);
            return vertices;
        }

        /// <summary>
        /// Gets all the pairs of points which distance is less or equal than the specified distance.
        /// </summary>
        /// <param name="radius">The maximum distance between two points. </param>
        /// <returns>The pairs of points which distance is less or equal than the specified distance.</returns>
        public List<Tuple<T, T>> ConnectAll(double radius)
        {
            List<Tuple<T, T>> connexions = new List<Tuple<T, T>>();
            GetConnexions(this.Root, radius * radius, connexions);
            return connexions;
        }

        #endregion

        #region Private methods

        private CurveVertexNode<T> Create(T[] vertices, int depth, CurveVertexNode<T> parent, bool isLeft)
        {
            int length = vertices.Length;
            if (length == 0) 
                return null;
            int d = depth % this._dimension;
            T median = vertices.QuickSelectMedian((p1, p2) => _pointSelector(p1)[d].CompareTo(_pointSelector(p2)[d]));
            var node = new CurveVertexNode<T>(median);
            node.Depth = depth;
            node.Parent = parent;
            node.IsLeft = isLeft;
            int mid = length / 2;
            int rlen = length - mid - 1;
            var left = new T[mid];
            var right = new T[rlen];
            Array.Copy(vertices, 0, left, 0, mid);
            Array.Copy(vertices, mid + 1, right, 0, rlen);
            if (depth < this._parallelDepth)
            {
                System.Threading.Tasks.Parallel.Invoke(
                   () => node.LeftChild = Create(left, depth + 1, node, true),
                   () => node.RightChild = Create(right, depth + 1, node, false)
                );
            }
            else
            {
                node.LeftChild = Create(left, depth + 1, node, true);
                node.RightChild = Create(right, depth + 1, node, false);
            }
            return node;
        }

        private T GetNeighbour(Point3d center, CurveVertexNode<T> node, T currentBest, double bestDist)
        {
            if (node == null)
                return currentBest;
            T current = node.Value;
            int d = node.Depth % this._dimension;
            double coordCen = center[d];
            double coordCur = _pointSelector(current)[d];
            double dist = this._sqrDist(center, _pointSelector(current));
            if (dist >= 0.0 && dist < bestDist)
            {
                currentBest = current;
                bestDist = dist;
            }
            dist = coordCen - coordCur;
            if (bestDist < dist * dist)
            {
                currentBest = GetNeighbour(
                    center, coordCen < coordCur ? node.LeftChild : node.RightChild, currentBest, bestDist);
                bestDist = this._sqrDist(center, _pointSelector(currentBest));
            }
            else
            {
                currentBest = GetNeighbour(center, node.LeftChild, currentBest, bestDist);
                bestDist = this._sqrDist(center, _pointSelector(currentBest));
                currentBest = GetNeighbour(center, node.RightChild, currentBest, bestDist);
                bestDist = this._sqrDist(center, _pointSelector(currentBest));
            }
            return currentBest;
        }

        private void GetNeighboursAtDistance(Point3d center, double radius, CurveVertexNode<T> node, List<T> vertices)
        {
            if (node == null) 
                return;
            var current = node.Value;
            double dist = this._sqrDist(center, _pointSelector(current));
            if (dist <= radius)
            {
                vertices.Add(current);
            }
            int d = node.Depth % this._dimension;
            double coordCen = center[d];
            double coordCur = _pointSelector(current)[d];
            dist = coordCen - coordCur;
            if (dist * dist > radius)
            {
                if (coordCen < coordCur)
                {
                    GetNeighboursAtDistance(center, radius, node.LeftChild, vertices);
                }
                else
                {
                    GetNeighboursAtDistance(center, radius, node.RightChild, vertices);
                }
            }
            else
            {
                GetNeighboursAtDistance(center, radius, node.LeftChild, vertices);
                GetNeighboursAtDistance(center, radius, node.RightChild, vertices);
            }
        }

        private void GetKNeighbours(Point3d center, int number, CurveVertexNode<T> node, List<Tuple<double, T>> pairs)
        {
            if (node == null) 
                return;

            var current = node.Value;
            double dist = this._sqrDist(center, _pointSelector(current));
            int cnt = pairs.Count;
            if (cnt == 0)
            {
                pairs.Add(new Tuple<double, T>(dist, current));
            }
            else if (cnt < number)
            {
                if (dist > pairs[0].Item1)
                {
                    pairs.Insert(0, new Tuple<double, T>(dist, current));
                }
                else
                {
                    pairs.Add(new Tuple<double, T>(dist, current));
                }
            }
            else if (dist < pairs[0].Item1)
            {
                pairs[0] = new Tuple<double, T>(dist, current);
                pairs.Sort((p1, p2) => p2.Item1.CompareTo(p1.Item1));
            }
            int d = node.Depth % this._dimension;
            double coordCen = center[d];
            double coordCur = _pointSelector(current)[d];
            dist = coordCen - coordCur;
            if (dist * dist > pairs[0].Item1)
            {
                if (coordCen < coordCur)
                {
                    GetKNeighbours(center, number, node.LeftChild, pairs);
                }
                else
                {
                    GetKNeighbours(center, number, node.RightChild, pairs);
                }
            }
            else
            {
                GetKNeighbours(center, number, node.LeftChild, pairs);
                GetKNeighbours(center, number, node.RightChild, pairs);
            }
        }

        private void FindRange(Point3d lowerLeft, Point3d upperRight, CurveVertexNode<T> node, List<T> vertices)
        {
            if (node == null)
                return;
            var current = node.Value;
            var currentPoint = _pointSelector(current);
            if (_ignoreZ)
            {
                if (currentPoint.X >= lowerLeft.X && currentPoint.X <= upperRight.X &&
                    currentPoint.Y >= lowerLeft.Y && currentPoint.Y <= upperRight.Y)
                    vertices.Add(current);
            }
            else
            {
                if (currentPoint.X >= lowerLeft.X && currentPoint.X <= upperRight.X &&
                    currentPoint.Y >= lowerLeft.Y && currentPoint.Y <= upperRight.Y &&
                    currentPoint.Z >= lowerLeft.Z && currentPoint.Z <= upperRight.Z)
                    vertices.Add(current);
            }

            int d = node.Depth % this._dimension;
            if (upperRight[d] < currentPoint[d])
                FindRange(lowerLeft, upperRight, node.LeftChild, vertices);
            else if (lowerLeft[d] > currentPoint[d])
                FindRange(lowerLeft, upperRight, node.RightChild, vertices);
            else
            {
                FindRange(lowerLeft, upperRight, node.LeftChild, vertices);
                FindRange(lowerLeft, upperRight, node.RightChild, vertices);
            }
        }

        private void GetConnexions(CurveVertexNode<T> node, double radius, List<Tuple<T, T>> connexions)
        {
            if (node == null) 
                return;
            var vertices = new List<T>();
            var center = node.Value;
            if (_ignoreZ)
                GetRightParentsNeighbours(_pointSelector(center), node, radius, vertices);
            GetNeighboursAtDistance(_pointSelector(center), radius, node.LeftChild, vertices);
            GetNeighboursAtDistance(_pointSelector(center), radius, node.RightChild, vertices);
            for (int i = 0; i < vertices.Count; i++)
            {
                connexions.Add(new Tuple<T, T>(center, vertices[i]));
            }
            GetConnexions(node.LeftChild, radius, connexions);
            GetConnexions(node.RightChild, radius, connexions);
        }

        private void GetRightParentsNeighbours(Point3d center, CurveVertexNode<T> node, double radius, List<T> vertices)
        {
            CurveVertexNode<T> parent = GetRightParent(node);
            if (parent == null) return;
            int d = parent.Depth % this._dimension;
            double dist = center[d] - _pointSelector(parent.Value)[d];
            if (dist * dist <= radius)
            {
                GetNeighboursAtDistance(center, radius, parent.RightChild, vertices);
            }
            GetRightParentsNeighbours(center, parent, radius, vertices);
        }

        private CurveVertexNode<T> GetRightParent(CurveVertexNode<T> node)
        {
            CurveVertexNode<T> parent = node.Parent;
            if (parent == null) return null;
            if (node.IsLeft) return parent;
            return GetRightParent(parent);
        }

        private double SqrDistance2d(Point3d p1, Point3d p2)
        {
            return (p1.X - p2.X) * (p1.X - p2.X) +
                (p1.Y - p2.Y) * (p1.Y - p2.Y);
        }

        private double SqrDistance3d(Point3d p1, Point3d p2)
        {
            return (p1.X - p2.X) * (p1.X - p2.X) +
                (p1.Y - p2.Y) * (p1.Y - p2.Y) +
                (p1.Z - p2.Z) * (p1.Z - p2.Z);
        }

        #endregion
    }

    static class Extensions
    {
        // Credit: Tony Tanzillo
        // http://www.theswamp.org/index.php?topic=44312.msg495808#msg495808
        public static T QuickSelectMedian<T>(this T[] items, Comparison<T> compare)
        {
            int l = items.Length;
            int k = l / 2;
            if (items == null || l == 0)
                throw new ArgumentException("array");
            int from = 0;
            int to = l - 1;
            while (from < to)
            {
                int r = from;
                int w = to;
                T current = items[(r + w) / 2];
                while (r < w)
                {
                    if (compare(items[r], current) > -1)
                    {
                        var tmp = items[w];
                        items[w] = items[r];
                        items[r] = tmp;
                        w--;
                    }
                    else
                    {
                        r++;
                    }
                }
                if (compare(items[r], current) > 0)
                {
                    r--;
                }
                if (k <= r)
                {
                    to = r;
                }
                else
                {
                    from = r + 1;
                }
            }
            return items[k];
        }
    }
}
