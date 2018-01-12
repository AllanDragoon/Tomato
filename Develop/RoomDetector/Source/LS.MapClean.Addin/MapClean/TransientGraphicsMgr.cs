using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Settings;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace LS.MapClean.Addin.MapClean
{
    /// <summary>
    /// Manage AutoCAD transient graphics for map clean.
    /// </summary>
    public class TransientGraphicsMgr
    {
        private List<Drawable> _drawables = new List<Drawable>();
        private const double PI = 3.1415926;

        #region APIs
        public void CreateTransientErrorMarks(MarkShape shapeType, Point3d[] positions, AcadColor color, Drawable[] transientEntities)
        {
            // Clear previous transient graphics first.
            ClearTransientGraphics();

            IEnumerable<Entity> drawables = null;
            switch (shapeType)
            {
                case MarkShape.Circle:
                    drawables = CreateCircleMarks(positions);
                    break;
                case MarkShape.Diamond:
                    drawables = CreateDiamondMarks(positions);
                    break;
                case MarkShape.Square:
                    drawables = CreateSquareMarks(positions);
                    break;
                case MarkShape.Triangle:
                    drawables = CreateTriangleMarks(positions);
                    break;
                case MarkShape.Cross:
                    drawables = CreateCrossMarks(positions);
                    break;
            }
            foreach (var drawable in drawables)
            {
                drawable.Color = color;
            }

            _drawables.AddRange(drawables);
            if (transientEntities != null)
            {
                foreach (Entity transientEntity in transientEntities)
                {
                    transientEntity.Color = color;
                }
                _drawables.AddRange(transientEntities);
            }
            foreach (var d in _drawables)
            {
                TransientManager.CurrentTransientManager.AddTransient(
                    d, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
            }
        }
        public void ClearTransientGraphics()
        {
            // Clear the transient graphics for our drawables
            bool success = TransientManager.CurrentTransientManager.EraseTransients(
                TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());

            if (success)
            {
                // Dispose of them and clear the list
                foreach (Drawable d in _drawables)
                {
                    d.Dispose();
                }
                _drawables.Clear();
            }
        }

        #endregion

        #region Methods

        private IEnumerable<Entity> CreateCircleMarks(Point3d[] positions)
        {
            var result = new List<Entity>();
            foreach (var position in positions)
            {
                var ents = CreateCircleMark(position);
                result.AddRange(ents);
            }
            return result;
        }
        private IEnumerable<Entity> CreateCircleMark(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();
            var circle = new Circle(position, new Vector3d(0, 0, 1.0), markSize / 2.0);
            result.Add(circle);

            var ents = CreatePointMarker(position);
            result.AddRange(ents);
            return result;
        }

        private IEnumerable<Entity> CreateTriangleMarks(Point3d[] positions)
        {
            var result = new List<Entity>();
            foreach (var position in positions)
            {
                var ents = CreateTriangleMark(position);
                result.AddRange(ents);
            }
            return result;
        }
        private IEnumerable<Entity> CreateTriangleMark(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();
            // Create polyline
            var vector = new Vector3d(1, 0, 0);
            var vector1 = vector.RotateBy(-PI / 6.0, Vector3d.ZAxis);
            var point1 = position + vector1 * markSize / 2.0;
            var vector2 = vector.RotateBy(PI / 2, Vector3d.ZAxis);
            var point2 = position + vector2 * markSize / 2.0;
            var vector3 = vector.RotateBy(-PI * 5.0 / 6.0, Vector3d.ZAxis);
            var point3 = position + vector3 * markSize / 2.0;
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            polyline.AddVertexAt(0, new Point2d(point1.X, point1.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(point2.X, point2.Y), 0, 0, 0 );
            polyline.AddVertexAt(2, new Point2d(point3.X, point3.Y), 0, 0, 0);
            polyline.Closed = true;
            result.Add(polyline);

            // Create point.
            var ents = CreatePointMarker(position);
            result.AddRange(ents);
            return result;
        }

        private IEnumerable<Entity> CreateDiamondMarks(Point3d[] positions)
        {
            var result = new List<Entity>();
            foreach (var position in positions)
            {
                var ents = CreateDiamondMark(position);
                result.AddRange(ents);
            }
            return result;
        }
        private IEnumerable<Entity> CreateDiamondMark(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();
            // Create polyline
            var point1 = position + new Vector3d(1, 0, 0) * markSize / 2.0;
            var point2 = position + new Vector3d(0, 1, 0) * markSize / 4.0;
            var point3 = position + new Vector3d(-1, 0, 0) * markSize / 2.0;
            var point4 = position + new Vector3d(0, -1, 0) * markSize / 4.0;
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            polyline.AddVertexAt(0, new Point2d(point1.X, point1.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(point2.X, point2.Y), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(point3.X, point3.Y), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(point4.X, point4.Y), 0, 0, 0);
            polyline.Closed = true;
            result.Add(polyline);

            // Create center point
            var ents = CreatePointMarker(position);
            result.AddRange(ents);

            return result;
        }

        private IEnumerable<Entity> CreateSquareMarks(Point3d[] positions)
        {
            var result = new List<Entity>();
            foreach (var position in positions)
            {
                var ents = CreateSquareMark(position);
                result.AddRange(ents);
            }
            return result;
        }
        private IEnumerable<Entity> CreateSquareMark(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();
            // Create polyline
            var vector = new Vector3d(1, 0, 0);
            var radius = markSize / 2.0;
            var point1 = position + vector.RotateBy(PI/4.0, Vector3d.ZAxis)*radius;
            var point2 = position + vector.RotateBy(PI*3.0/4.0, Vector3d.ZAxis)*radius;
            var point3 = position + vector.RotateBy(PI*5.0/4.0, Vector3d.ZAxis)*radius;
            var point4 = position + vector.RotateBy(PI*7.0/4.0, Vector3d.ZAxis)*radius;
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            polyline.AddVertexAt(0, new Point2d(point1.X, point1.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(point2.X, point2.Y), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(point3.X, point3.Y), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(point4.X, point4.Y), 0, 0, 0);
            polyline.Closed = true;
            result.Add(polyline);

            // Create center point
            var ents = CreatePointMarker(position);
            result.AddRange(ents);
            return result;
        }

        private IEnumerable<Entity> CreateCrossMarks(Point3d[] positions)
        {
            var result = new List<Entity>();
            foreach (var position in positions)
            {
                var ents = CreateCrossMark(position);
                result.AddRange(ents);
            }
            return result;
        }
        public IEnumerable<Entity> CreateCrossMark(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();

            var vector1 = new Vector3d(1, 0, 0);
            var vector2 = new Vector3d(0, 1, 0);
            var radius = markSize / 2.0;
            var point1 = position + vector1 * radius;
            var point2 = position - vector1 * radius;
            var point3 = position + vector2 * radius;
            var point4 = position - vector2 * radius;
            var polyline1 = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            var polyline2 = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            polyline1.AddVertexAt(0, new Point2d(point1.X, point1.Y), 0, 0, 0);
            polyline1.AddVertexAt(1, new Point2d(point2.X, point2.Y), 0, 0, 0);
            polyline2.AddVertexAt(0, new Point2d(point3.X, point3.Y), 0, 0, 0);
            polyline2.AddVertexAt(1, new Point2d(point4.X, point4.Y), 0, 0, 0);
            result.Add(polyline1);
            result.Add(polyline2);
            return result;
        }

        private IEnumerable<Entity> CreatePointMarker(Point3d position)
        {
            var markSize = ErrorMarkSettings.CurrentSettings.MarkerSize;
            var result = new List<Entity>();
            var halfSize = markSize / 16.0;
            var point1 = position - Vector3d.XAxis * halfSize;
            var point2 = position + Vector3d.XAxis * halfSize;
            var point3 = position - Vector3d.YAxis * halfSize;
            var point4 = position + Vector3d.YAxis * halfSize;
            var line1 = new Line(point1, point2);
            var line2 = new Line(point3, point4);
            result.Add(line1);
            result.Add(line2);
            return result;
        }
        #endregion
    }
}
