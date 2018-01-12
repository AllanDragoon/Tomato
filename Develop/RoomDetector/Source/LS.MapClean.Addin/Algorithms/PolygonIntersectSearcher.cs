using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using ClipperLib;
using LS.MapClean.Addin.Utils;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace LS.MapClean.Addin.Algorithms
{
    public struct PolygonIntersect
    {
        /// <summary>
        /// Source polygon Id.
        /// </summary>
        public ObjectId SourceId { get; set; }

        /// <summary>
        /// Target polygon Id.
        /// </summary>
        public ObjectId TargetId { get; set; }

        ///// <summary>
        ///// Intersection paths.
        ///// </summary>
        //public List<Point3d[]> IntersectionPaths { get; set; }

        public List<Polyline> Intersections { get; set; }

        ///// <summary>
        ///// Source intersection points.
        ///// </summary>
        //public Point3d[] SourceIntersectPoints { get; set; }

        ///// <summary>
        ///// Target intersect points.
        ///// </summary>
        //public Point3d[] TargetIntersectPoints { get; set; }
    }

    public class PolygonIntersectSearcher : AlgorithmWithEditor
    {
        private IEnumerable<PolygonIntersect> _intersects = new PolygonIntersect[0];
        public IEnumerable<PolygonIntersect> Intersects
        {
            get { return _intersects; }
        }

        public PolygonIntersectSearcher(Editor editor)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var database = Editor.Document.Database;
            var allVertices = new List<CurveVertex>();
            var curveIds = new List<ObjectId>();
            using (var transaction = database.TransactionManager.StartTransaction())
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
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point, ignoreZ: true);

            // Use kd tree to check include.
            var intersects = new List<PolygonIntersect>();
            var analyzed = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in curveIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    var extents = curve.GeometricExtents;
                    var nearVertices = kdTree.BoxedRange(extents.MinPoint, extents.MaxPoint);

                    foreach (var curveVertex in nearVertices)
                    {
                        if (curveVertex.Id == objectId ||
                            analyzed.Contains(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id)))
                        {
                            continue;
                        }

                        analyzed.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        var polygonIntersect = AnalyzePolygonIntersection(objectId, curveVertex.Id, transaction);
                        if (polygonIntersect != null)
                        {
                            var sourceId = polygonIntersect.Value.SourceId;
                            var targetId = polygonIntersect.Value.TargetId;
                            var existing = intersects.FirstOrDefault(it => it.SourceId == sourceId && it.TargetId == targetId ||
                                                                     it.SourceId == targetId && it.TargetId == sourceId);
                            if (existing.Equals(default(PolygonIntersect)))
                                intersects.Add(polygonIntersect.Value);
                        }
                    }
                }
                transaction.Commit();
            }
            _intersects = intersects;
        }

        private Polyline CreatePolygon(Point3d[] points)
        {
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            polyline.Closed = true;
            return polyline;
        }

        private PolygonIntersect? AnalyzePolygonIntersection(ObjectId sourceId, ObjectId targetId, Transaction transaction)
        {
            var sourceCurve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
            var targetCurve = transaction.GetObject(targetId, OpenMode.ForRead) as Curve;
            if (!IsCurveClosed(sourceCurve) || !IsCurveClosed(targetCurve))
                return null;
            
            // Use clipper to calculate the intersection
            var precision = 0.000001;
            var subject = new List<List<IntPoint>>(1);
            var clipper = new List<List<IntPoint>>(1);
            var result = new List<List<IntPoint>>();

            var sourceVertices = CurveUtils.GetDistinctVertices(sourceCurve, transaction);
            var targetVertices = CurveUtils.GetDistinctVertices(targetCurve, transaction);

            var subjectPath = sourceVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            var clipperPath = targetVertices.Select(it => new IntPoint(it.X / precision, it.Y / precision)).ToList();
            subject.Add(subjectPath);
            clipper.Add(clipperPath);
            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            cpr.Execute(ClipType.ctIntersection, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            if (result.Count <= 0)
            {
                //Editor.WriteMessage("有相交，但Clipper没有计算出相交部分");
                return null;
            }

            var intersectPaths = new List<Polyline>();
            foreach (var path in result)
            {
                var points = path.Select(it => new Point3d(it.X * precision, it.Y * precision, 0.0)).ToArray();
                var polyline = CreatePolygon(points);
                // If the polygon's area is very small, just ignore, or it will bother user.
                if (polyline.Area.Smaller(0.001))
                {
                    polyline.Dispose();
                    continue;
                }
                intersectPaths.Add(polyline);
            }

            if (intersectPaths.Count <= 0)
                return null;

            return new PolygonIntersect()
            {
                SourceId = sourceId,
                TargetId = targetId,
                Intersections = intersectPaths
            };
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
