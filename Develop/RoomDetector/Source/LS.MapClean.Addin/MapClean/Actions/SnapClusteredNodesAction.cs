using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class SnapClusteredNodesAction : MapCleanActionBase
    {
        public SnapClusteredNodesAction(Document document)
            : base(document)
        {
            Tolerance = 2.0;
        }

        public override ActionType ActionType
        {
            get { return ActionType.SnapClustered; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            //var results = new List<SnapClusteredNodesCheckResult>();
            //var editor = Document.Editor;
            //var clusteredNodesSnaper = new ClusteredNodesSnaper(editor, Tolerance);
            //clusteredNodesSnaper.Check();
            //if (clusteredNodesSnaper.ClusteredNodes == null)
            //    return results;

            //foreach (var clusteredNode in clusteredNodesSnaper.ClusteredNodes)
            //{
            //    var sourceIds = clusteredNode.Value.Select(position => position.Curve.ObjectId).ToList();
            //    var checkResult = new SnapClusteredNodesCheckResult(clusteredNode, sourceIds);
            //    results.Add(checkResult);
            //}

            //return results;

            var results = new List<ClusteredNodesCheckResult>();
            var editor = Document.Editor;
            var snaper = new KdTreeClusteredNodesSnaper(editor, Tolerance);
            snaper.Check(selectedObjectIds);
            if (snaper.ClusterNodesInfos == null)
                return results;

            foreach (var info in snaper.ClusterNodesInfos)
            {
                var checkResult = new ClusteredNodesCheckResult(info);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var clusteredNodesCheckResult = checkResult as ClusteredNodesCheckResult;
            if (clusteredNodesCheckResult == null)
                return Status.Rejected;

            var clusteredNodesInfo = clusteredNodesCheckResult.ClusteredNodes;

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                // 每个clusteredNode的Postions都移动到key（Point3d）的位置, 暂时只处理Polyline
                foreach (var curveVertex in clusteredNodesInfo.Vertices)
                {
                    var curve = transaction.GetObject(curveVertex.Id, OpenMode.ForWrite) as Polyline;
                    if (curve == null)
                        continue;

                    // 比较curve的start point和end point到聚合点的距离。哪个短就在它的位置上插入一个节点用来连接线和聚合点
                    if ((curve.StartPoint - curveVertex.Point).Length < (curve.EndPoint - curveVertex.Point).Length)
                    {
                        double bulge = curve.GetBulgeAt(0);
                        curve.RemoveVertexAt(0);
                        curve.AddVertexAt(0, new Point2d(curveVertex.Point.X, curveVertex.Point.Y), bulge, 0, 0);
                    }
                    else
                    {
                        double bulge = curve.GetBulgeAt(curve.NumberOfVertices - 1);
                        curve.RemoveVertexAt(curve.NumberOfVertices - 1);
                        curve.AddVertexAt(curve.NumberOfVertices, new Point2d(curveVertex.Point.X, curveVertex.Point.Y), bulge, 0, 0);
                    }
                }
                transaction.Commit();
            }
            return Status.Fixed;
        }
    }
}
