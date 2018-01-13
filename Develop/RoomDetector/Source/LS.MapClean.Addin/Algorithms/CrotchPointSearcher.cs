using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Algorithms
{
    public class CrotchPointSearcher
    {
        public static Dictionary<ObjectId, List<Point3d>> GetCrotchPoints(Database database,
            IEnumerable<ObjectId> parcelIds)
        {
            var result = new Dictionary<ObjectId, List<Point3d>>();
            // Create a kd tree.
            var allVertices = new List<CurveVertex>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objId in parcelIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    allVertices.AddRange(vertices.Select(it => new CurveVertex(it, objId)));
                }
                transaction.Commit();
            }
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);

            // 搜索三岔口
            //using (var tolerance = new ToleranceOverrule(null))
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var parcelId in parcelIds)
                {
                    var curve = transaction.GetObject(parcelId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    var ptLink = LinkedPoint.GetLinkedPoints(curve, transaction, isLoop: true);
                    var ptTraverse = ptLink;
                    // Use records to improve performance.
                    var records = new Dictionary<LinkedPoint, IEnumerable<CurveVertex>>();
                    while (ptTraverse != null)
                    {
                        var prev = ptTraverse.Prev;
                        var next = ptTraverse.Next;
                        if (!records.ContainsKey(prev))
                            records[prev] = kdTree.NearestNeighbours(prev.Point.ToArray(), radius: 0.001);
                        if (!records.ContainsKey(ptTraverse))
                            records[ptTraverse] = kdTree.NearestNeighbours(ptTraverse.Point.ToArray(), radius: 0.001);
                        if (!records.ContainsKey(next))
                            records[next] = kdTree.NearestNeighbours(next.Point.ToArray(), radius: 0.001);

                        foreach (var vertex in records[ptTraverse])
                        {
                            if (result.ContainsKey(parcelId) && result[parcelId].Contains(vertex.Point))
                                continue;

                            if (vertex.Id == parcelId || vertex.Point != ptTraverse.Point)
                                continue;
                            var prevVertex = new CurveVertex(prev.Point, vertex.Id);
                            var nextVertex = new CurveVertex(next.Point, vertex.Id);
                            if (records[prev].Contains(prevVertex) && records[next].Contains(nextVertex))
                                continue;

                            List<Point3d> list = null;
                            if (result.ContainsKey(parcelId))
                                list = result[parcelId];
                            else
                            {
                                list = new List<Point3d>();
                                result[parcelId] = list;
                            }
                            list.Add(ptTraverse.Point);
                        }

                        ptTraverse = ptTraverse.Next;
                        if (ptTraverse == ptLink)
                            break;
                    }
                }
                transaction.Commit();
            }

            return result; 
        }
    }
}
