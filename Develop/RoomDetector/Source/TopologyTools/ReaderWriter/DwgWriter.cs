using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace TopologyTools.ReaderWriter
{
    public class DwgWriter : GeometryWriter
    {
		public DwgWriter()
		{
            AllowRepeatedCoordinates = true;
		}

		public Point3d WritePoint3D(Coordinate coordinate)
		{
			Point3d result;
			if (!double.IsNaN(coordinate.Z))
			{
				result = new Point3d(this.PrecisionModel.MakePrecise(coordinate.X), 
                    this.PrecisionModel.MakePrecise(coordinate.Y), this.PrecisionModel.MakePrecise(coordinate.Z));
				return result;
			}
			result = new Point3d(this.PrecisionModel.MakePrecise(coordinate.X), this.PrecisionModel.MakePrecise(coordinate.Y), 0.0);
			return result;
		}

		public Point3d WritePoint3D(IPoint point)
		{
			return this.WritePoint3D(point.Coordinate);
		}

        public Point3dCollection WritePoint3DCollection(MultiPoint multiPoint)
        {
            var points = new Point3dCollection();
            foreach (IGeometry geometry in multiPoint)
            {
                var point = geometry as Point;
                if (point == null)
                    continue;
                points.Add(this.WritePoint3D(point));
            }
            return points;
        }

		public Point2d WritePoint2D(Coordinate coordinate)
		{
			return new Point2d(this.PrecisionModel.MakePrecise(coordinate.X), this.PrecisionModel.MakePrecise(coordinate.Y));
		}

		public Point2d WritePoint2D(IPoint point)
		{
			return this.WritePoint2D(point.Coordinate);
		}

        public Point2dCollection WritePoint2DCollection(MultiPoint multiPoint)
        {
            var points = new Point2dCollection();
            foreach (IGeometry geometry in multiPoint)
            {
                var point = geometry as Point;
                if (point == null)
                    continue;
                points.Add(this.WritePoint2D(point));
            }
            return points;
        }

		public DBPoint WriteDbPoint(IPoint point)
		{
			return new DBPoint(this.WritePoint3D(point));
		}

        public IList<DBPoint> WriteDbPoints(MultiPoint multiPoint)
        {
            var dbPoints = new List<DBPoint>();
            foreach (IGeometry geometry in multiPoint)
            {
                var point = geometry as Point;
                if (point == null)
                    continue;
                dbPoints.Add(new DBPoint(this.WritePoint3D(point)));
            }
            return dbPoints;
        }

		public Polyline WritePolyline(ILineString lineString)
		{
			var polyline = new Polyline();
            for (var i = 0; i < lineString.Coordinates.Length; i++)
			{
                var coordinate = lineString.Coordinates[i];
				var point2D = new Point2d(coordinate.X, coordinate.Y);
                polyline.AddVertexAt(i, point2D, 0.0, 0.0, 0.0);
			}

			polyline.Closed = lineString.StartPoint.EqualsExact(lineString.EndPoint);
			polyline.MinimizeMemory();
			return polyline;
		}

		public Polyline WritePolyline(ILinearRing linearRing)
		{
			var polyline = new Polyline();
            for (var i = 0; i < linearRing.Coordinates.Length; i++)
			{
                var coordinate = linearRing.Coordinates[i];
				polyline.AddVertexAt(i, this.WritePoint2D(coordinate), 0.0, 0.0, 0.0);
			}
			polyline.Closed = true;
			polyline.MinimizeMemory();
			return polyline;
		}

		public Polyline[] WritePolyline(IPolygon polygon)
		{
            var list = new List<Polyline> {this.WritePolyline(polygon.Shell)};
		    ILinearRing[] holes = polygon.Holes;
            for (var i = 0; i < holes.Length; i++)
			{
                var linearRing = (LinearRing)holes[i];
				list.Add(this.WritePolyline(linearRing));
			}
			return list.ToArray();
		}

        public Polyline[] WritePolyline(IMultiPolygon multiPolygon)
        {
            var result = new List<Polyline>();
            foreach (var subGeom in multiPolygon.Geometries)
            {
                var polygon = subGeom as Polygon;
                if (polygon != null)
                {
                    var polylines = WritePolyline(polygon);
                    result.AddRange(polylines);
                }
            }
            return result.ToArray();
        }

		public Polyline3d WritePolyline3D(ILineString lineString)
		{
			var point3DCollection = new Point3dCollection();
            var coordinates = lineString.Coordinates; 
            foreach (var coordinate in coordinates)
                point3DCollection.Add(this.WritePoint3D(coordinate));
		    return new Polyline3d(0, point3DCollection, lineString.StartPoint.EqualsExact(lineString.EndPoint));
		}

		public Polyline3d WritePolyline3D(ILinearRing linearRing)
		{
            var point3DCollection = new Point3dCollection();
            var coordinates = linearRing.Coordinates;
            foreach (var coordinate in coordinates)
                point3DCollection.Add(this.WritePoint3D(coordinate));
		    return new Polyline3d(0, point3DCollection, true);
		}

		public Polyline2d WritePolyline2D(ILineString lineString)
		{
            var point3DCollection = new Point3dCollection();
            var coordinates = lineString.Coordinates;
            foreach (var coordinate in coordinates)
                point3DCollection.Add(this.WritePoint3D(coordinate));
			return new Polyline2d(0, point3DCollection, 0.0, lineString.StartPoint.EqualsExact(lineString.EndPoint), 0.0, 0.0, null);
		}

        public Polyline WritePolyline(IMultiLineString lineString)
        {
            var polyline = new Polyline();
            for (var i = 0; i < lineString.Coordinates.Length; i++)
            {
                var coordinate = lineString.Coordinates[i];
                var point2D = new Point2d(coordinate.X, coordinate.Y);
                polyline.AddVertexAt(i, point2D, 0.0, 0.0, 0.0);
            }

            //polyline.Closed = lineString.StartPoint.EqualsExact(lineString.EndPoint);
            polyline.MinimizeMemory();
            return polyline;
        }

        public Polyline2d WritePolyline2D(IMultiLineString lineString)
        {
            var point3DCollection = new Point3dCollection();
            var coordinates = lineString.Coordinates;
            foreach (var coordinate in coordinates)
                point3DCollection.Add(this.WritePoint3D(coordinate));
            return new Polyline2d(0, point3DCollection, 0.0, false, 0.0, 0.0, null);
        }

		public Polyline2d WritePolyline2D(ILinearRing linearRing)
		{
			var point3DCollection = new Point3dCollection();
			var coordinates = linearRing.Coordinates;
            foreach (var coordinate in coordinates)
                point3DCollection.Add(this.WritePoint3D(coordinate));
            return new Polyline2d(0, point3DCollection, 0.0, true, 0.0, 0.0, null);
		}

		public Line WriteLine(LineSegment lineSegment)
		{
			var line = new Line
			{
			    StartPoint = WritePoint3D(lineSegment.P0), 
                EndPoint = WritePoint3D(lineSegment.P1)
			};
		    return line;
		}

        public MPolygon WriteMPolygon(IPolygon polygon)
		{
			var mPolygon = new MPolygon();
			var mpolygonLoopCollection = GetMPolygonLoopCollection(polygon);
			try
			{
                foreach (var mpolygonLoop in mpolygonLoopCollection)
			    {
                    var mPolygonLoop = (MPolygonLoop)mpolygonLoop;
					mPolygon.AppendMPolygonLoop(mPolygonLoop, false, 0.0);
				}
			}
			finally
			{
			    foreach (var mpolygonLoop in mpolygonLoopCollection)
			    {
			        if (mpolygonLoop is IDisposable)
			        {
			            (mpolygonLoop as IDisposable).Dispose();
			        }
			    }
			}
			mPolygon.BalanceTree();
			return mPolygon;
		}

		public MPolygon WriteMPolygon(MultiPolygon multiPolygon)
		{
			var mPolygon = new MPolygon();
            var geometries = multiPolygon.Geometries;

			for (int i = 0; i < geometries.Length; i++)
			{
                var polygon = (IPolygon)geometries[i];
                var mpolygonLoopCollection = GetMPolygonLoopCollection(polygon);
				try
				{
                    foreach (var mpolygonLoop in mpolygonLoopCollection)
                    {
                        var mPolygonLoop = (MPolygonLoop)mpolygonLoop;
                        mPolygon.AppendMPolygonLoop(mPolygonLoop, false, 0.0);
                    }
				}
				finally
				{
                    foreach (var mpolygonLoop in mpolygonLoopCollection)
                    {
                        if (mpolygonLoop is IDisposable)
                        {
                            (mpolygonLoop as IDisposable).Dispose();
                        }
                    }
				}
			}
			mPolygon.BalanceTree();
			return mPolygon;
		}

        public Entity WriteEntity(string className, IGeometry geometry)
        {
            if (className == "AcDbMPolygon")
            {
                return this.WriteMPolygon((Polygon) geometry);
            }
            if (className == "AcDbPoint" || className == "AcDbBlockReference")
			{
			    return this.WriteDbPoint((Point)geometry);
			}
            if (className == "AcDbLine"
                || className == "AcDbPolyline"
                || className == "AcDb2dPolyline"
                || className == "AcDb3dPolyline"
                || className == "AcDbMline"
                || className == "AcDb2dPolyline"
                || className == "AcDb2dPolyline")
            {
                return this.WritePolyline((LineString)geometry);
            }

            return null;
        }

		MPolygonLoop GetMPolygonLoop(ILinearRing linearRing)
		{
            var mPolygonLoop = new MPolygonLoop();
			var coordinates = linearRing.Coordinates;
            for (var i = 0; i < coordinates.Length; i++)
			{
                mPolygonLoop.Add(new BulgeVertex(this.WritePoint2D(coordinates[i]), 0.0));
			}
			return mPolygonLoop;
		}

		private MPolygonLoopCollection GetMPolygonLoopCollection(IPolygon polygon)
		{
            var mPolygonLoopCollection = new MPolygonLoopCollection {this.GetMPolygonLoop(polygon.Shell)};
		    var holes = polygon.Holes;
			for (var i = 0; i < holes.Length; i++)
			{
				ILinearRing linearRing = holes[i];
				mPolygonLoopCollection.Add(this.GetMPolygonLoop(linearRing));
			}
			return mPolygonLoopCollection;
		}
	}
}
