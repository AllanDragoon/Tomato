using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClipperLib;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// 独立的区域单元
    /// </summary>
    public struct IsolatedRegion
    {
        /// <summary>
        /// 分区的外轮廓
        /// </summary>
        public List<Point3d> Contour { get; set; }

        /// <summary>
        /// 区域中的相互连接的Curve Ids
        /// </summary>
        public IEnumerable<ObjectId> CurveIds { get; set; }

    }

    /// <summary>
    /// DWG分区：将图中各自连接的多边形作为一个区域单元，最终分区结果是，DWG被
    /// 分割成一个一个不相互连接的区域。
    /// </summary>
    public class DrawingPartitioner : AlgorithmWithDatabase
    {
        private IEnumerable<IsolatedRegion> _isolatedRegions = new IsolatedRegion[0];
        public IEnumerable<IsolatedRegion> IsolatedRegions
        {
            get { return _isolatedRegions; }
        }

        public DrawingPartitioner(Database database)
            : base(database)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return;

            var regions = new List<IsolatedRegion>();
            using (var transaction = Database.TransactionManager.StartTransaction())
            {
                // Create a kdtree, for near searching
                var kdTree = CreatePolygonKdTree(selectedObjectIds, transaction);
                var visited = new HashSet<ObjectId>();

                foreach (var objectId in selectedObjectIds)
                {
                    if (visited.Contains(objectId))
                        continue;

                    // 防止二次访问，造成无限循环
                    visited.Add(objectId);

                    var contour = CurveUtils.GetDistinctVertices(objectId, transaction);
                    var idsGroup = new List<ObjectId>(){ objectId };
                    var unionableIds = new List<ObjectId>(){objectId};
                    while (unionableIds.Count > 0)
                    {
                        var newUnionableIds = new List<ObjectId>();
                        foreach (var unionableId in unionableIds)
                        {
                            // Get near polygon Ids
                            var nearIds = GetNearPolygonIds(kdTree, unionableId, visited, transaction);
                            foreach (var nearId in nearIds)
                            {
                                var nearPoints = CurveUtils.GetDistinctVertices(nearId, transaction);
                                List<Point3d> unionPolygon = null;
                                var unionable = AnalyzePolygonsUnionable(contour, nearPoints, out unionPolygon);
                                if (unionable)
                                {
                                    newUnionableIds.Add(nearId);
                                    visited.Add(nearId);
                                    idsGroup.Add(nearId);
                                    contour = unionPolygon;
                                }
                            }
                        }

                        unionableIds = newUnionableIds;
                    }

                    regions.Add(new IsolatedRegion()
                    {
                        Contour = contour,
                        CurveIds = idsGroup
                    });
                }
                transaction.Commit();
            }

            _isolatedRegions = regions;
        }

        CurveVertexKdTree<CurveVertex> CreatePolygonKdTree(IEnumerable<ObjectId> objectIds, Transaction transaction)
        {
            var vertices = new List<CurveVertex>();
            foreach (var objectId in objectIds)
            {
                var points = CurveUtils.GetDistinctVertices(objectId, transaction);
                if(points[0] == points[points.Count-1])
                    points.RemoveAt(points.Count - 1);
                vertices.AddRange(points.Select(it => new CurveVertex(it, objectId)));
            }

            var kdTree = new CurveVertexKdTree<CurveVertex>(vertices, it => it.Point.ToArray(), ignoreZ: true);
            return kdTree;
        }

        IEnumerable<ObjectId> GetNearPolygonIds(CurveVertexKdTree<CurveVertex> kdTree, 
            ObjectId polygonId, HashSet<ObjectId> visitedIds, Transaction transaction)
        {
            var result = new HashSet<ObjectId>();
            var curve = (Curve) transaction.GetObject(polygonId, OpenMode.ForRead);
            var extents = curve.GeometricExtents;
            var dir = (extents.MaxPoint - extents.MinPoint).GetNormal();
            // 向外扩张0.1
            var minPoint = extents.MinPoint - dir*0.1;
            var maxPoint = extents.MaxPoint + dir*0.1;
            var nearVertices = kdTree.BoxedRange(minPoint.ToArray(), maxPoint.ToArray());
            foreach (var curveVertex in nearVertices)
            {
                // 不要包含本身
                if (curveVertex.Id == polygonId || visitedIds.Contains(curveVertex.Id))
                    continue;

                result.Add(curveVertex.Id);
            }
            return result;
        }

        bool AnalyzePolygonsUnionable(List<Point3d> sourcePolygon, List<Point3d> targetPolygon,
            out List<Point3d> unionPolygon)
        {
            unionPolygon = sourcePolygon;
            var unionResult = MinimalLoopSearcher2.ClipperBoolean(new List<List<Point3d>>() {sourcePolygon},
                new List<List<Point3d>>() {targetPolygon}, ClipType.ctUnion);
            if (unionResult.Count > 1)
            {
                if (unionResult.Count == 2)
                {
                    if((PolygonIncludeSearcher.AreIdenticalCoordinates(sourcePolygon, unionResult[0]) ||
                        PolygonIncludeSearcher.AreIdenticalCoordinates(sourcePolygon, unionResult[1])) )
                    return false;
                }

                unionResult = unionResult
                    .OrderByDescending(it => ComputerGraphics.PolygonArea(it.ToArray()))
                    .ToList();
                var source = new List<List<Point3d>>()
                {
                    unionResult[0]
                };
                // 如果存在多于一个布尔运算结果的情况，需要分析他们是否有包含关系
                for (int i = 1; i < unionResult.Count; i++)
                {
                    var subUnionResult = MinimalLoopSearcher2.ClipperBoolean(
                        source, new List<List<Point3d>>() {unionResult[i]}, ClipType.ctUnion);
                    if (subUnionResult.Count > 1 || subUnionResult.Count <= 0)
                        return false;
                    source = subUnionResult;
                }

                unionResult = source;
            }

            if (unionResult.Count != 1)
                return false;
            unionPolygon = unionResult[0];
            return true;
        }
    }
}
