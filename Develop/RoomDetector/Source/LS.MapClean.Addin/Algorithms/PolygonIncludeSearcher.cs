using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ClipperLib;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public class PolygonIncludeSearcher : AlgorithmWithDatabase
    {
        private List<KeyValuePair<ObjectId, ObjectId>> _includePolygons =
            new List<KeyValuePair<ObjectId, ObjectId>>();
        public IEnumerable<KeyValuePair<ObjectId, ObjectId>> IncludePolygons
        {
            get { return _includePolygons; }
        }

        public PolygonIncludeSearcher(Database database)
            : base(database)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var allVertices = new List<CurveVertex>();
            var curveIds = new List<ObjectId>();
            using (var transaction = this.Database.TransactionManager.StartTransaction())
            {
                foreach (var objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    curveIds.Add(objId);
                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    allVertices.AddRange(vertices.Select(it => new CurveVertex(it, objId)));
                }
                transaction.Commit();
            }
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);

            // Use kd tree to check include.
            var analyzed = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            using (var transaction = this.Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in curveIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    var extents = curve.GeometricExtents;
                    var nearVertices = kdTree.BoxedRange(extents.MinPoint.ToArray(), extents.MaxPoint.ToArray());
                    
                    foreach (var curveVertex in nearVertices)
                    {
                        if (curveVertex.Id == objectId || 
                            analyzed.Contains(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id)))
                        { 
                            continue;
                        }

                        analyzed.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        var targetCurve = transaction.GetObject(curveVertex.Id, OpenMode.ForRead) as Curve;
                        if (IsInclude(curve, targetCurve, transaction))
                        {
                            _includePolygons.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        }
                    }
                }
                transaction.Commit();
            }
        }

        //public static bool IsInclude(Curve sourceCurve, Curve targetCurve, Transaction transaction)
        //{
        //    var sourceVertices = CurveUtils.GetDistinctVertices(sourceCurve, transaction).ToList();
        //    var sourceVertices2d = sourceVertices.Select(it => new Point2d(it.X, it.Y)).ToList();
        //    if (sourceVertices2d[0] != sourceVertices2d[sourceVertices2d.Count - 1])
        //        sourceVertices2d.Add(sourceVertices2d[0]);
        //    var sourcePolygon = sourceVertices2d.ToArray();

        //    var targetVertices = CurveUtils.GetDistinctVertices(targetCurve, transaction).ToList();
        //    foreach (var targetVertex in targetVertices)
        //    {
        //        var point2D = new Point2d(targetVertex.X, targetVertex.Y);
        //        if (!ComputerGraphics.IsInPolygon(sourcePolygon, point2D, sourcePolygon.Length - 1))
        //            return false;
        //    }

        //    // There is a case, a polyon's all vertices are on another concave polygon, but the former is not in the latter.
        //    // So we need to use the former's edge middle points to check.
        //    if (targetVertices[0] != targetVertices[targetVertices.Count - 1])
        //        targetVertices.Add(targetVertices[0]);
        //    var middlePoints = GetAllMiddlePoints(targetVertices);
        //    foreach (var middlePoint in middlePoints)
        //    {
        //        var point2D = new Point2d(middlePoint.X, middlePoint.Y);
        //        if (!ComputerGraphics.IsInPolygon(sourcePolygon, point2D, sourcePolygon.Length - 1))
        //            return false;
        //    }

        //    return true;
        //}

        public static bool IsInclude(Curve sourceCurve, Curve targetCurve, Transaction transaction)
        {
            var sourceVertices = CurveUtils.GetDistinctVertices(sourceCurve, transaction);
            var targetVertices = CurveUtils.GetDistinctVertices(targetCurve, transaction);
            return IsInclude(sourceVertices, targetVertices);
        }

        public static bool IsInclude(List<Point3d> sourceVertices, List<Point3d> targetVertices)
        {
            // Use clipper to calculate the intersection
            var precision = 0.000001;
            var subject = new List<List<IntPoint>>(1);
            var clipper = new List<List<IntPoint>>(1);
            var result = new List<List<IntPoint>>();

            var subjectPath = sourceVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            subject.Add(subjectPath);

            var clipperPath = targetVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            clipper.Add(clipperPath);

            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            cpr.Execute(ClipType.ctIntersection, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            // If include, result.Count == 1
            if (result.Count != 1)
            {
                return false;
            }

            var path = result[0];
            var points = path.Select(it => new Point3d(it.X * precision, it.Y * precision, 0.0)).ToList();
            // 转换过程中会有精度损失，因此在调用AreDuplicateEntities比较的时候用newTargetVertices。
            var newTargetVertices = clipperPath.Select(it => new Point3d(it.X * precision, it.Y * precision, 0.0)).ToList();
            if (AreDuplicateEntities(newTargetVertices, points))
                return true;
            return false;
        }

        public static bool AreDuplicateEntities(List<Point3d> source, List<Point3d> target)
        {
            if (source == null || source.Count == 0 || target == null || target.Count == 0)
                return false;

            // 如果他们坐标完全一样，那么认为他们是相同的，无需再用clipperlib
            if (AreIdenticalCoordinates(source, target.ToList()))
                return true;

            // 有时候坐标太大，导致计算结果不正确，所以移到原点去计算
            var vector = Point3d.Origin - source[0];
            source = source.Select(it => it + vector).ToList();
            target = target.Select(it => it + vector).ToList();

            // Use clipper to calculate the intersection
            var precision = 0.0001; // 精度不能太大，否则结果出错
            var subject = new List<List<IntPoint>>(1);
            var clipper = new List<List<IntPoint>>(1);
            var result = new List<List<IntPoint>>();

            // 用Math.Rount解决精度带来的影响。
            var subjectPath = source.Select(it => new IntPoint(Math.Round(it.X / precision), Math.Round(it.Y / precision))).ToList();
            subject.Add(subjectPath);

            var clipperPath = target.Select(it => new IntPoint(Math.Round(it.X / precision), Math.Round(it.Y / precision))).ToList();
            clipper.Add(clipperPath);

            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            // 相同为0，相异为1
            cpr.Execute(ClipType.ctXor, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            if (result.Count == 0)
                return true;
            foreach (var path in result)
            {
                var points = path.Select(it => new Point3d(it.X * precision, it.Y * precision, 0.0)).ToArray();
                var polyline = CurveUtils.CreatePolygon(points);
                // If the polygon's area is very small, just ignore, or it will bother user.
                if (polyline.Area.Larger(0.001))
                {
                    polyline.Dispose();
                    return false;
                }
                polyline.Dispose();
            }
            return true;
        }

        public static bool AreIdenticalCoordinates(List<Point3d> source, List<Point3d> target)
        {
            if (source.Count != target.Count)
                return false;

            // 判断序列中每个点是否相同
            bool same = true;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != target[i])
                {
                    same = false;
                    break;
                }
            }

            if (same)
                return true;

            // 反转一下再判断一次
            target.Reverse();
            same = true;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != target[i])
                {
                    same = false;
                    break;
                }
            }
            return same;
        }

        private static IEnumerable<Point3d> GetAllMiddlePoints(List<Point3d> vertices)
        {
            var result = new List<Point3d>();
            if (vertices.Count < 2)
                return result;
            var previous = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                var current = vertices[i];
                var middlePoint = previous + (current - previous) / 2;
                result.Add(middlePoint);
                previous = current;
            }
            return result;
        }

        private bool IsCurveClosed(Curve curve)
        {
            bool closed = false;
            var polyline = curve as Polyline;
            var polyline2d = curve as Polyline2d;
            if (polyline != null)
                closed = polyline.Closed;
            else if (polyline2d != null)
                closed = polyline2d.Closed;
            return closed;
        }
    }
}
