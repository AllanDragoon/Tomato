using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ClipperLib;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public class PolygonHoleSearcher : AlgorithmWithEditor
    {
        private readonly List<Polyline> _holes = new List<Polyline>();
        public IEnumerable<Polyline> Holes
        {
            get { return _holes; }
        }

        public PolygonHoleSearcher(Editor editor)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<Autodesk.AutoCAD.DatabaseServices.ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var precision = 0.000001;
            // First we need to make sure all intersections are vertices of polygon
            var missingVertexSearcher = new MissingVertexSearcher(Editor, precision);
            missingVertexSearcher.Check(selectedObjectIds);
            if (missingVertexSearcher.MissingVertexInfos.Any())
            {
                missingVertexSearcher.FixAll();
            }

            // Use clipper to search holes
            var subject = new List<List<IntPoint>>(1);
            var clipper = new List<List<IntPoint>>(1);

            var database = Editor.Document.Database;
            Extents3d extents = new Extents3d(new Point3d(0,0,0), new Point3d(1,1,0));
            bool first = true;

            // Use all polygons to make up clipper.
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    // Calculate its extents.
                    if (first)
                    {
                        extents = curve.GeometricExtents;
                        first = false;
                    }
                    else
                    {
                        extents.AddExtents(curve.GeometricExtents);
                    }

                    // Add it to the clipper.
                    var vertices = CurveUtils.GetDistinctVertices2D(curve, transaction);
                    // Only for polygon.
                    if (vertices.Count() < 3)
                        continue;

                    // That has the same vertex for the first and last array members.
                    if (vertices[0] != vertices[vertices.Count - 1])
                        vertices.Add(vertices[0]);
                    var clockwise = ComputerGraphics.ClockWise2(vertices.ToArray());
                    if (!clockwise)
                        vertices.Reverse();
                    if (vertices[0] == vertices[vertices.Count - 1])
                        vertices.RemoveAt(vertices.Count - 1);

                    clipper.Add(vertices.Select(it => new IntPoint(it.X / precision, it.Y/precision)).ToList());
                }
                transaction.Commit();
            }

            // Create subject rectangle.
           
            var vector = (extents.MaxPoint - extents.MinPoint)*0.1;
            var minPoint = extents.MinPoint - vector;
            var maxPoint = extents.MaxPoint + vector;
            subject.Add(new List<IntPoint>()
            {
                new IntPoint(minPoint.X/precision, minPoint.Y/precision),
                new IntPoint(minPoint.X/precision, maxPoint.Y/precision),
                new IntPoint(maxPoint.X/precision, maxPoint.Y/precision),
                new IntPoint(maxPoint.X/precision, minPoint.Y/precision)
            });


            var result = new List<List<IntPoint>>();
            var cpr = new Clipper();
            cpr.AddPaths(subject, PolyType.ptSubject, true);
            cpr.AddPaths(clipper, PolyType.ptClip, true);
            cpr.Execute(ClipType.ctDifference, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
            if (result.Count <= 0)
            {
                return;
            }

            foreach (var path in result)
            {
                // Ignore the outmost loop.
                if (path.Contains(new IntPoint(minPoint.X/precision, minPoint.Y/precision)))
                    continue;

                var points = path.Select(it => new Point2d(it.X * precision, it.Y * precision)).ToList();
                if (points[0] != points[points.Count - 1])
                    points.Add(points[0]);
                var array = points.ToArray();
                if (ComputerGraphics.ClockWise2(array))
                {
                    continue;
                }

                var polyline = CreatePolygon(array);
                if (polyline.Area.Smaller(0.001))
                {
                    polyline.Dispose();
                    continue;
                }

                _holes.Add(polyline);
            }
        }

        private Polyline CreatePolygon(Point2d[] points)
        {
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(i, points[i], 0, 0, 0);
            polyline.Closed = true;
            return polyline;
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
