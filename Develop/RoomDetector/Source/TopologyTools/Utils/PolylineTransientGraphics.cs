using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using GeoAPI.Geometries;

namespace TopologyTools.Utils
{
    public static class PolylineTransientGraphics
    {
        const int DefaultColorIndex = 4;
        const double lineWidth = 0.05;

        public static void CreateTransientRegions(Database database, List<Region> regions)
        {
            ClearTransientGraphics(ref _drawables);

            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var region in regions)
                {
                    region.ColorIndex = DefaultColorIndex;
                    _drawables.Add(region);

                    // Draw each one initially
                    TransientManager.CurrentTransientManager.AddTransient(
                        region, TransientDrawingMode.DirectShortTerm,
                        128, new IntegerCollection()
                    );
                }

                tr.Commit();
            }
        }

        public static void CreateTransientLines(Database database, List<IGeometry> geometries)
        {
            ClearTransientGraphics(ref _drawables);

            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var geom in geometries)
                {
                    var intersects = new List<Point3d>();
                    foreach (var Coordinate in geom.Coordinates)
                    {
                        intersects.Add(new Point3d(Coordinate.X, Coordinate.Y, 0));
                    }
                    CreateTransientLines(database, intersects);
                }

                tr.Commit();
            }
        }

        public static void CreateTransientLines(Database database, List<Point3d> points)
        {
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            foreach (var point3D in points)
            {
                var pointNow = new Point2d(point3D.X, point3D.Y);
                polyline.AddVertexAt(polyline.NumberOfVertices, pointNow, 0, lineWidth, lineWidth);
            }

            polyline.ColorIndex = DefaultColorIndex;
            _drawables.Add(polyline);

            // ???
            //polyline.Dispose();

            // Draw each one initially
            foreach (Drawable d in _drawables)
            {
                TransientManager.CurrentTransientManager.AddTransient(
                    d, TransientDrawingMode.DirectShortTerm,
                    128, new IntegerCollection()
                    );
            }
        }

        private static List<Drawable> _drawables = new List<Drawable>();

        public static void ClearTransientGraphics()
        {
            ClearTransientGraphics(ref _drawables);
        }

        public static void ClearTransientGraphics(ref List<Drawable> drawables)
        {
            // Clear the transient graphics for our drawables
            if (drawables.Count < 1)
                return;

            TransientManager tm = TransientManager.CurrentTransientManager;
            var intCol = new IntegerCollection();
            if (drawables != null)
            {
                foreach (Drawable drawable in drawables)
                {
                    tm.EraseTransient(drawable, intCol);
                    drawable.Dispose();
                }
                drawables.Clear();
            }
        }
    }
}
