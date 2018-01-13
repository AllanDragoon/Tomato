using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.Algorithms
{
    public class ClusterNodesInfo
    {
        public CurveVertex[] Vertices { get; set; }
    }

    public class KdTreeClusteredNodesSnaper : AlgorithmWithEditor
    {
        private double _tolerance;

        private IEnumerable<ClusterNodesInfo> _clusterNodesInfos;
        public IEnumerable<ClusterNodesInfo> ClusterNodesInfos
        {
            get { return _clusterNodesInfos; }
        }

        public KdTreeClusteredNodesSnaper(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return;

            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Search dangling vertices first.
                var danglingSearcher = new KdTreeDanglingVertexSearcher(selectedObjectIds, forDrawing: true, transaction: transaction);
                var danglingVertices = danglingSearcher.Search();

                // Traverse all dangling vertices and search the cluster nodes.
                _clusterNodesInfos = GetClusterNodesKdTree(danglingVertices, transaction);
            
                transaction.Commit();
            }

            watch.Stop();
            var elapsedMS = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n花费时间{0}毫秒", elapsedMS);
        }

        public void Fix()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<ClusterNodesInfo> GetClusterNodesKdTree(IEnumerable<CurveVertex> danglingVertices, Transaction transaction)
        {
            var result = new List<ClusterNodesInfo>();
            var kdTree = new CurveVertexKdTree<CurveVertex>(danglingVertices, it=>it.Point.ToArray(), ignoreZ: true);
            // Get all connections of kdTree
            foreach (var vertex in danglingVertices)
            {
                var neighbors = kdTree.NearestNeighbours(vertex.Point.ToArray(), _tolerance);
                if (!neighbors.Any())
                    continue;

                // Remove some invalid vertices.
                var list = neighbors.ToList();
                int currentIndex = 0;
                while (currentIndex < list.Count - 1)
                {
                    var current = list[currentIndex];
                    for (int i = currentIndex + 1; i < list.Count; i++)
                    {
                        var dist = current.Point.DistanceTo(list[i].Point);
                        if (dist.Larger(_tolerance))
                        {
                            list.RemoveAt(currentIndex);
                            list.RemoveAt(i-1);
                            break;
                        }
                    }
                    currentIndex++;
                }

                if (list.Count > 1)
                {
                    var node = new ClusterNodesInfo()
                    {
                        Vertices = list.ToArray()
                    };
                    result.Add(node);
                }
            }

            return result;
        }
    }
    /// <summary>
    /// “SnapClusteredNodes”以更正同一点附近的多个节点。使用“SnapClusteredNodes”，可以找到彼此间隙在指定距离内的节点，然后将其捕捉到一个位置（这几个点boundary的中心）。
    /// 此清理动作包含多段线起始点/终点处的节点。
    /// </summary>
    public class ClusteredNodesSnaper : AlgorithmWithEditor
    {
        public enum PositionEnum
        {
            Start,
            End
        }

        public struct Position
        {
            public Position(Point3d mPoint, PositionEnum mPositionEnum, Curve curve)
            {
                _mPoint = mPoint;
                _positionEnum = mPositionEnum;
                _mCurve = curve;
            }

            private Point3d _mPoint;
            private PositionEnum _positionEnum;
            private Curve _mCurve;

            public Point3d Point
            {
                get { return _mPoint; }
            }

            public PositionEnum PositionEnum
            {
                get { return _positionEnum; }
            }

            public Curve Curve
            {
                get { return _mCurve; }
            }
        }

        private double _tolernace = 0.0;

        private Dictionary<Point3d, List<Position>> _clusteredNodes;

        public Dictionary<Point3d, List<Position>> ClusteredNodes
        {
            get { return _clusteredNodes; }
        }

        public ClusteredNodesSnaper(Editor editor, double tolerance) : base(editor)
        {
            _tolernace = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            _clusteredNodes = GetClusteredNodes(db, _tolernace, selectedObjectIds);
        }

        public void Fix()
        {
            var layers = new List<String> { "0" };
            Database db = Application.DocumentManager.MdiActiveDocument.Database;

            // Fix就是将clusteredNode移动到同一点上.
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (KeyValuePair<Point3d, List<Position>> clusteredNode in _clusteredNodes)
                {
                    // 每个clusteredNode的Postions都移动到key（Point3d）的位置, 暂时只处理Polyline
                    foreach (Position position in clusteredNode.Value)
                    {
                        var curve = trans.GetObject(position.Curve.ObjectId, OpenMode.ForWrite) as Polyline;
                        if (curve == null)
                            continue;

                        if (position.PositionEnum == PositionEnum.Start)
                        {
                            double bulge = curve.GetBulgeAt(0);
                            curve.RemoveVertexAt(0);
                            curve.AddVertexAt(0, new Point2d(clusteredNode.Key.X, clusteredNode.Key.Y), bulge, 0, 0);
                        }
                        else
                        {
                            double bulge = curve.GetBulgeAt(curve.NumberOfVertices - 1);
                            curve.RemoveVertexAt(curve.NumberOfVertices - 1);
                            curve.AddVertexAt(curve.NumberOfVertices, new Point2d(clusteredNode.Key.X, clusteredNode.Key.Y), bulge, 0, 0);
                        }
                    }
                }
                trans.Commit();
            }

        }

        private Dictionary<Point3d, List<Position>> GetClusteredNodes(Database database, double tolernace, IEnumerable<ObjectId> selectedObjectIds)
        {
            var clusteredNodes = new Dictionary<Point3d, List<Position>>();

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var allPositions = new List<Position>();

                foreach (var objectId in selectedObjectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all specified layers curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        allPositions.Add(new Position(curve.StartPoint, PositionEnum.Start, curve));
                        allPositions.Add(new Position(curve.EndPoint, PositionEnum.End, curve));
                    }
                }

                var tempallPositionsList = new List<Position>(allPositions);

                // 检查两个Positon之间的距离，如果比minimalLength小，但有没有相交，则认为是clusteredNode.
                for (int i = 0; i < allPositions.Count; i++)
                {
                    // 检查是否当前Position已经被添加到clusteredNodes了，如果添加过，则跳过。
                    if (!tempallPositionsList.Contains(allPositions[i]))
                        continue;
                    for (int j = 0; j < allPositions.Count; j++)
                    {
                        // 检查是否当前Position已经被添加到clusteredNodes了，如果添加过，则跳过。
                        if (!tempallPositionsList.Contains(allPositions[j]))
                            continue;

                        // 跳过自己。
                        if (i == j)
                            continue;

                        var positionsDistance = (allPositions[i].Point - allPositions[j].Point).Length;
                        if (positionsDistance < tolernace && positionsDistance > Tolerance.Global.EqualPoint)
                        {
                            if (clusteredNodes.ContainsKey(allPositions[i].Point))
                            {
                                clusteredNodes[allPositions[i].Point].Add(allPositions[i]);
                                clusteredNodes[allPositions[i].Point].Add(allPositions[j]);
                            }
                            else
                            {
                                clusteredNodes.Add(allPositions[i].Point, new List<Position> { allPositions[i], allPositions[j] });
                            }

                            // 从临时列表中删除已经添加到clusteredNodes的Postions。
                            tempallPositionsList.Remove(allPositions[i]);
                            tempallPositionsList.Remove(allPositions[j]);
                        }
                    }
                }

                trans.Abort();
            }

            return clusteredNodes;
        }

    }
}
