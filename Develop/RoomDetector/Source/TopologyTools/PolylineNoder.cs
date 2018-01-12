using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TopologyTools.Utils;

namespace TopologyTools
{
    public static class PolylineNoder
    {
        public const double PointTolerance = 1e-03;
        public static List<Point3d> GetGeometryNodes(Document document, double buffer)
        {
            var polylineIds = CadUtils.FindAllPolylines(document);
            var nears = GetNearGeometries(polylineIds.ToList(), buffer);
            var result = GetPolylineNodingResult(document.Database, nears, true);
            return result.NodePoints;
        }

        public static List<Point3d> GetGeometryNodes(IList<ObjectId> objectIds, double buffer)
        {
            if (objectIds.Count == 0)
                return new List<Point3d>();

            var database = objectIds[0].Database;
            var nears = GetNearGeometries(objectIds, buffer);
            var result = GetPolylineNodingResult(database, nears, true);
            return result.NodePoints;
        }

        public static List<Point3d> GetGeometryNodes(Database database, IList<ObjectId> objectIds)
        {
            var nears = GetNearGeometries(objectIds);
            var result = GetPolylineNodingResult(database, nears, true);
            return result.NodePoints;
        }

        public static Dictionary<ObjectId, IList<ObjectId>> GetNearGeometries(double buffer = 0.5)
        {
            // 避免误差太小（现在是1e-10），现在变小一点
            // 过滤选择polyline
            var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            // 计算相邻多边形，以0.5作为buffer
            return NtsUtils.GetNearGeometries(polylineIds.ToList(), buffer);
        }

        public static Dictionary<ObjectId, IList<ObjectId>> GetNearGeometries(IList<ObjectId> objectIds, double buffer = 0.5)
        {
            // 计算相邻多边形，以0.5作为buffer
            return NtsUtils.GetNearGeometries(objectIds, buffer);
        }

        public class PolylineNodingResult
        {
            public List<Point3d> NodePoints { get; set; }
            public Dictionary<ObjectId, IList<Point3d>> MissingPoints { get; set; }

            public PolylineNodingResult()
            {
                NodePoints = new List<Point3d>();
                MissingPoints = new Dictionary<ObjectId, IList<Point3d>>();
            }

            public void AddMissingPoint(ObjectId objectId, Point3d point)
            {
                if (!MissingPoints.ContainsKey(objectId))
                    MissingPoints.Add(objectId, new List<Point3d>());

                //var point2D = new Point2d(point.X, point.Y);
                bool flag = MissingPoints[objectId].ToArray().Cast<object>().Any(t => ((Point3d)t).DistanceTo(point) < 1e-06);

                // 先看看有没有已经生成好, 如果没有生成，直接加上界址点
                if (!flag)
                {
                    MissingPoints[objectId].Add(point);
                }
            }
        }

        public static Dictionary<ObjectId, IList<Point3d>> FindMissingVertex(IList<ObjectId> objectIds, double tolerance)
        {
            if (objectIds.Count == 0)
                return new Dictionary<ObjectId, IList<Point3d>>();

            var database = objectIds[0].Database;
            var nears = GetNearGeometries(objectIds, 0.5);
            var result = GetPolylineNodingResult(database, nears, false, tolerance);
            return result.MissingPoints;
        }

        public static PolylineNodingResult GetPolylineNodingResult(Database database, 
            Dictionary<ObjectId, IList<ObjectId>> nears, bool autoNoding, double tolerance = PointTolerance)
        {
            // 避免误差太小（现在是1e-10），现在变小一点
            using (new ToleranceOverrule(1e-05))
            using (var tr = database.TransactionManager.StartTransaction())
            {
                // 计算三岔口
                var handledPairs = new HashSet<string>();
                var result = new PolylineNodingResult();
                // 遍历每个多边形
                foreach (var near in nears)
                {
                    // 遍历多边形的每一个邻接多边形
                    foreach (ObjectId objectId in near.Value)
                    {
                        var nearId = near.Key;

                        // 记录当前处理的object对，这样避免重复计算一对相邻的多边形
                        var handle = objectId + "_" + nearId;
                        var handle2 = nearId + "_" + objectId;
                        if (!handledPairs.Contains(handle) && !handledPairs.Contains(handle2))
                        {
                            handledPairs.Add(handle);
                            var potentialNodes = FindPotentialNodes(tr, objectId, nearId);

                            if (autoNoding)
                                NodingPolylines(tr, objectId, nearId, potentialNodes, tolerance);

                            // 检查是否存在不在顶点上的多边形
                            var curve1 = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                            var curve2 = tr.GetObject(nearId, OpenMode.ForRead) as Curve;
                            foreach (var point3D in potentialNodes)
                            {
                                if (!CheckPointIsVertex(curve1, point3D, tolerance))
                                    result.AddMissingPoint(curve1.ObjectId, point3D);
                                if (!CheckPointIsVertex(curve2, point3D, tolerance))
                                    result.AddMissingPoint(curve2.ObjectId, point3D);
                            }
                            result.NodePoints.AddRange(potentialNodes);
                        }
                    }
                }

                tr.Commit();
                return result;
            }
        }

        static void NodingPolylines(Transaction tr, ObjectId id1, ObjectId id2, IEnumerable<Point3d> points, double tolerance)
        {
            var curve1 = tr.GetObject(id1, OpenMode.ForRead) as Curve;
            var curve2 = tr.GetObject(id2, OpenMode.ForRead) as Curve;

            foreach (var point3D in points)
            {
                EnsurePointIsVertex(tr, curve1, point3D, tolerance);
                EnsurePointIsVertex(tr, curve2, point3D, tolerance);
            }
        }

        static void EnsurePointIsVertex(Transaction tr, Curve curve, Point3d point, double tolerance)
        {
            var param = CadUtils.SafeGetParameterAtPoint(curve, point);
            var paramBase = (int)Math.Floor(param);
            if (param - Math.Floor(param) > Math.Ceiling(param) - param)
            {
                paramBase = (int)Math.Ceiling(param);
            }
            if (Math.Abs(paramBase - param) > tolerance) // parameter 不是整数
            {
                curve.UpgradeOpen();
                AddVertex.AddVertexFromPolyline(tr, curve, point);
                curve.DowngradeOpen();
            }
        }

        static bool CheckPointIsVertex(Curve curve, Point3d point, double tolerance)
        {
            var param = CadUtils.SafeGetParameterAtPoint(curve, point);
            var paramBase = (int)Math.Floor(param);
            if (param - Math.Floor(param) > Math.Ceiling(param) - param)
            {
                paramBase = (int)Math.Ceiling(param);
            }
            var abs = Math.Abs(paramBase - param);
            return abs < tolerance; // parameter 不是整数
        }

        private static int Number = 1;
        public static List<Point3d> FindPotentialNodes(Transaction tr, ObjectId id1, ObjectId id2)
        {
            var curve1 = tr.GetObject(id1, OpenMode.ForRead) as Curve;
            var curve2 = tr.GetObject(id2, OpenMode.ForRead) as Curve;

            if(curve1 == null || curve2 == null)
                return new List<Point3d>();
            
            var points = CadUtils.IntersectWith(curve1.Database, curve1.ObjectId, curve2.ObjectId);

            if (points.Count == 0)
                return new List<Point3d>();

            //CadUtils.AddName(database, tr, curve1, "curve1");
            //CadUtils.AddName(database, tr, curve2, "curve2");

            const double paramTolerance = 0.0001;
            var result = new List<Point3d>();
            foreach (Point3d point in points)
            {
                // 如果这里tolerance非常小，会弹出异常，导致无法取得点的参数？
                var param = CadUtils.SafeGetParameterAtPoint(curve1, point);

                // 我们来看看这个点是不是三岔口
                // 办法是看看左边一点点，或者右边一点点，是不是还在curve2上，
                // 如果是，那不是三岔口，是邻接边，
                // 如果不是，就是三岔口，不是邻接边
                double param1 = param + paramTolerance;

                // 如果大于EndParam，用EndParam减了，这样的话，得到的是小于EndParam的参数
                // 比如12.1 - 12 = 0.1
                if (param1 > curve1.EndParam)
                    param1 = param1 - curve1.EndParam;

                double param2 = param - paramTolerance;
                // 如果是负数，用EndParam加过去，这样的话，得到的是大于0的参数
                // 比如-0.1 + 12 = 11.9
                if (param2 < 0)
                    param2 = curve1.EndParam + param2;

                // 取到两个相邻的点
                var point1 = CadUtils.SafeGetPointAtParameter(curve1, param1);
                var point2 = CadUtils.SafeGetPointAtParameter(curve1, param2);

                // 如果偏移后的两个点不都在curve2上面，应该是三岔口。
                // 注意：这里的容差不能是Tolerance.Global.EqualPoint，这个过于精确。
                // 我们的经验值大概是0.001，也可以考虑给客户去设定。
                if (!CadUtils.IsPointOnCurveGCP(curve2, point1)
                    || !CadUtils.IsPointOnCurveGCP(curve2, point2))
                {
                    //CadUtils.DrawPoint(tr, database, point, 2);
                    result.Add(point);
                }
            }
            return result;
        }

        // 辅助函数，可以用来画画点
        public static void DrawNodes(ObjectId id1, ObjectId id2)
        {
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var result = FindPotentialNodes(tr, id1, id2);
                DrawPoints(tr, database, result);
                tr.Commit();
            }
        }

        public static void DrawPoints(Transaction tr, Database database, List<Point3d> points)
        {
            foreach (var point3D in points)
                CadUtils.DrawPoint(tr, database, point3D, 2);
        }

        public static void DrawPoints(Database database, List<Point3d> points)
        {
            using (var tr = database.TransactionManager.StartTransaction())
            {
                DrawPoints(tr, database, points);
                tr.Commit();
            }
        }
    }
}
