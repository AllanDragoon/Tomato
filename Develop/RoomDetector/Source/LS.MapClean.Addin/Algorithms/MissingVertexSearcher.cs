using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using TopologyTools;

namespace LS.MapClean.Addin.Algorithms
{
    public class MissingVertexInfo
    {
        public ObjectId PolylineId { get; set; }
        public List<Point3d> Positions { get; set; }

        public MissingVertexInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    [Obsolete]
    public class MissingVertexSearcher : AlgorithmWithEditor
    {
        private List<MissingVertexInfo> _missingVertexInfos = new List<MissingVertexInfo>();
        public IEnumerable<MissingVertexInfo> MissingVertexInfos
        {
            get { return _missingVertexInfos; }
        }

        // TODO: temporarily
        private double _tolerance = 1e-3;
        public MissingVertexSearcher(Editor editor)
            : base(editor)
        {
            
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var database = Editor.Document.Database;
            var group = CalculateIntersection(selectedObjectIds, true, database);

            // Check each intersection whether it's a missing vertex.
            // TODO: 用一个比较低的精度去比较一个交点是否是顶点
            using (var tolerance = new SafeToleranceOverride(_tolerance, _tolerance))
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var pair in group)
                {
                    var curve = transaction.GetObject(pair.Key, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    var noneVertexPoints = new List<Point3d>();
                    foreach (var point in pair.Value)
                    {
                        var ret = CheckPointIsVertex(vertices, point, transaction);
                        if(!ret)
                            noneVertexPoints.Add(point);
                    }

                    if (noneVertexPoints.Count > 0)
                    {
                        _missingVertexInfos.Add(new MissingVertexInfo()
                        {
                            PolylineId = pair.Key,
                            Positions = noneVertexPoints
                        });
                    }
                }
                transaction.Commit();
            }
        }

        public bool FixAll()
        {
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var missingVertexInfo in MissingVertexInfos)
                {
                    var curve = transaction.GetObject(missingVertexInfo.PolylineId, OpenMode.ForWrite) as Curve;
                    foreach (var position in missingVertexInfo.Positions)
                    {
                        AddVertex.AddVertexFromPolyline(transaction, curve, position);
                    }
                }
                transaction.Commit();
            }
            return true;
        }

        private bool CheckPointIsVertex(IEnumerable<Point3d> vertices , Point3d point, Transaction transaction)
        {
            foreach (var point3D in vertices)
            {
                if (point3D == point)
                    return true;
            }
            return false;
        }

        public static Dictionary<ObjectId, List<Point3d>> CalculateIntersection(IEnumerable<ObjectId> objectIds,
            bool includeInline, Database database)
        {
            IEnumerable<IntersectionInfo> intersections = null;
            // 调低计算精度，否则有些交叉因为精度问题算不出来
            var oldTolerance = DoubleExtensions.STolerance;
            DoubleExtensions.STolerance = 1e-05;
            using (var tolerance = new SafeToleranceOverride(DoubleExtensions.STolerance))
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Build curve bsp tree and search all intersections
                var curve2dBspBuilder = new Curve2dBspBuilder(objectIds, transaction);
                intersections = curve2dBspBuilder.SearchRealIntersections(includeInline: includeInline);
                transaction.Commit();
            }
            // 恢复默认的计算精度值
            DoubleExtensions.STolerance = oldTolerance;

            // Group intersections by object id.
            var group = new Dictionary<ObjectId, List<Point3d>>();
            foreach (var intersection in intersections)
            {
                if (!group.ContainsKey(intersection.SourceId))
                    group[intersection.SourceId] = new List<Point3d>();
                var points = group[intersection.SourceId];
                if (!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);

                if (intersection.SourceId == intersection.TargetId)
                    continue;

                if (!group.ContainsKey(intersection.TargetId))
                    group[intersection.TargetId] = new List<Point3d>();
                points = group[intersection.TargetId];
                if (!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);
            }
            return group;
        }
    }

    public class MissingVertexSearcherQuadTree : AlgorithmWithEditor
    {
        private List<MissingVertexInfo> _missingVertexInfos = new List<MissingVertexInfo>();
        public IEnumerable<MissingVertexInfo> MissingVertexInfos
        {
            get { return _missingVertexInfos; }
        }

        public MissingVertexSearcherQuadTree(Editor editor)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var group = CalculateIntersectionKdTree(selectedObjectIds, true, Editor);

            // Check each intersection whether it's a missing vertex.
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var pair in group)
                {
                    var curve = transaction.GetObject(pair.Key, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    var noneVertexPoints = new List<Point3d>();
                    foreach (var point in pair.Value)
                    {
                        var ret = CheckPointIsVertex(vertices, point, transaction);
                        if (!ret)
                            noneVertexPoints.Add(point);
                    }

                    if (noneVertexPoints.Count > 0)
                    {
                        _missingVertexInfos.Add(new MissingVertexInfo()
                        {
                            PolylineId = pair.Key,
                            Positions = noneVertexPoints
                        });
                    }
                }
                transaction.Commit();
            }

            //foreach (var info in _missingVertexInfos)
            //{
            //    Editor.WriteMessage("\n{0}:", info.PolylineId.Handle.ToString());
            //    foreach (var point in info.Positions)
            //    {
            //        Editor.WriteMessage(" {0} ", point.ToString());
            //    }
            //    Editor.WriteMessage("\n");
            //}
        }

        public bool FixAll()
        {
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var missingVertexInfo in MissingVertexInfos)
                {
                    var curve = transaction.GetObject(missingVertexInfo.PolylineId, OpenMode.ForWrite) as Curve;
                    foreach (var position in missingVertexInfo.Positions)
                    {
                        AddVertex.AddVertexFromPolyline(transaction, curve, position);
                    }
                }
                transaction.Commit();
            }
            return true;
        }

        private bool CheckPointIsVertex(IEnumerable<Point3d> vertices, Point3d point, Transaction transaction)
        {
            foreach (var point3D in vertices)
            {
                if (point3D == point)
                    return true;
            }
            return false;
        }

        public static Dictionary<ObjectId, List<Point3d>> CalculateIntersectionQuadTree(IEnumerable<ObjectId> objectIds,
            bool includeInline, Editor editor)
        {
            var algorithm = new BreakCrossingObjectsQuadTree(editor);
            var intersections = algorithm.SearchRealIntersections(objectIds, includeInline);
            
            // Group intersections by object id.
            var group = GroupIntersections(intersections);
            return group;
        }

        public static Dictionary<ObjectId, List<Point3d>> CalculateIntersectionKdTree(IEnumerable<ObjectId> objectIds,
            bool includeInline, Editor editor)
        {
            var segmentPairs = PolygonGapSearcherKdTree.SearchNearSegments(objectIds, 0.1);
            var intersections = BreakCrossingObjectsQuadTree.GetIntersectionsFromNearPairs(segmentPairs, includeInline);

            // Group intersections by object id.
            var group = GroupIntersections(intersections);
            return group;
        }

        private static Dictionary<ObjectId, List<Point3d>> GroupIntersections(IEnumerable<IntersectionInfo> intersections)
        {
            // Group intersections by object id.
            var group = new Dictionary<ObjectId, List<Point3d>>();
            foreach (var intersection in intersections)
            {
                if (!group.ContainsKey(intersection.SourceId))
                    group[intersection.SourceId] = new List<Point3d>();
                var points = group[intersection.SourceId];
                if (!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);

                if (intersection.SourceId == intersection.TargetId)
                    continue;

                if (!group.ContainsKey(intersection.TargetId))
                    group[intersection.TargetId] = new List<Point3d>();
                points = group[intersection.TargetId];
                if (!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);
            }
            return group;
        }
    }
}
