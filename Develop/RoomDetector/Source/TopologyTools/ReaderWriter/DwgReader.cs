using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TopologyTools.ReaderWriter
{
    public class DwgReader : GeometryReader
    {
		public DwgReader()
		{
		    AllowRepeatedCoordinates = true;
		}

        public Coordinate ReadCoordinate(Point3d point3D)
		{
			return new Coordinate(this.PrecisionModel.MakePrecise(point3D.X), 
                this.PrecisionModel.MakePrecise(point3D.Y), 
                this.PrecisionModel.MakePrecise(point3D.Z));
		}

        public Coordinate ReadCoordinate(Point2d point2D)
		{
			return new Coordinate(this.PrecisionModel.MakePrecise(point2D.X), this.PrecisionModel.MakePrecise(point2D.Y));
		}

		public IPoint ReadPoint(DBPoint dbPoint)
		{
			return this.GeometryFactory.CreatePoint(this.ReadCoordinate(dbPoint.Position));
		}

		public IPoint ReadPoint(BlockReference blockReference)
		{
			return this.GeometryFactory.CreatePoint(this.ReadCoordinate(blockReference.Position));
		}

        public IGeometry ReadEntityAsGeometry(Transaction tr, ObjectId objectId)
        {
            var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
            if (curve == null || !curve.Visible)
                return null;

            // 图层关闭，继续，因为有可能是图幅层
            var layer = (LayerTableRecord)tr.GetObject(curve.LayerId, OpenMode.ForRead);
            if (layer.IsOff)
                return null;

            IGeometry geom = null;
            if (curve.Closed && NumberOfVerticesMoreThan3(curve))
            {
                geom = ReadCurveAsPolygon(tr, curve) as Polygon;
            }
            else
            {
                geom = ReadCurveAsLineString(tr, curve) as LineString;
            }
            if (geom != null)
                geom.UserData = objectId;
            return geom;
        }

        public IGeometry ReadEntityAsPolygon(Transaction tr, ObjectId objectId)
        {
            var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
            if (curve == null || !curve.Visible)
                return null;

            // 图层关闭，继续，因为有可能是图幅层
            var layer = (LayerTableRecord)tr.GetObject(curve.LayerId, OpenMode.ForRead);
            if (layer.IsOff)
                return null;

            IGeometry geom = null;
            if (curve.Closed && NumberOfVerticesMoreThan3(curve))
            {
                geom = ReadCurveAsPolygon(tr, curve) as Polygon;
            }
            if (geom != null)
                geom.UserData = objectId;
            return geom;
        }

        public ILineString ReadCurveAsLineString(Transaction tr, Curve curve)
        {
            var polyline = curve as Polyline;
            if (polyline != null)
                return ReadLineString(polyline);

            var polyline2d = curve as Polyline2d;
            if (polyline2d != null)
                return ReadLineString(tr, polyline2d);

            var polyline3d = curve as Polyline3d;
            if (polyline3d != null)
                return ReadLineString(tr, polyline3d);

            return null;
        }

        public ILineString ReadLineString(Polyline polyline)
		{
            var coordinateList = ReadCoordination(polyline);
            if (coordinateList.Count > 1)
            {
                Coordinate[] linePts = CoordinateArrays.RemoveRepeatedPoints(coordinateList.ToCoordinateArray());
                if (linePts.Count() > 1)
                    return this.GeometryFactory.CreateLineString(linePts);
            }
			return LineString.Empty;
		}

		public ILineString ReadLineString(Transaction tr, Polyline3d polyline3D)
		{
			var coordinateList = new CoordinateList();
            if (polyline3D.PolyType == Poly3dType.SimplePoly) // 0??
			{
			    foreach (var v in polyline3D)
			    {
			        PolylineVertex3d vertex = null;
			        if (v is ObjectId)
			        {
			            vertex = tr.GetObject((ObjectId) v, OpenMode.ForRead) as PolylineVertex3d;
			        }
                    else if (v is PolylineVertex3d)
                    {
                        vertex = (PolylineVertex3d) v;
                    }

			        if (vertex != null)
			        {
                        coordinateList.Add(this.ReadCoordinate(vertex.Position), this.AllowRepeatedCoordinates);
			        }
			    }
			}
			else
			{
                var dBObjectCollection = new DBObjectCollection();
				polyline3D.Explode(dBObjectCollection);
				try
				{
                    foreach (var dBObject in dBObjectCollection)
				    {
                        var line = (Line)dBObject;
                        coordinateList.Add(this.ReadCoordinate(line.StartPoint), false);
                        coordinateList.Add(this.ReadCoordinate(line.EndPoint), false);
				    }
				}
				finally
				{
                    foreach (var dBObject in dBObjectCollection)
                    {
                        if (dBObject is IDisposable)
                            (dBObject as IDisposable).Dispose();
					}
				}
				dBObjectCollection.Dispose();
			}

			if (polyline3D.Closed)
			{
				coordinateList.Add(coordinateList[0]);
			}
			if (coordinateList.Count > 1)
			{
				return this.GeometryFactory.CreateLineString(coordinateList.ToCoordinateArray());
			}
			return LineString.Empty;
		}

		public ILineString ReadLineString(Transaction tr, Polyline2d polyline2D)
		{
		    var coordinateList = ReadCoordination(polyline2D, tr);
            if (coordinateList.Count > 1)
            {
                Coordinate[] linePts = CoordinateArrays.RemoveRepeatedPoints(coordinateList.ToCoordinateArray());
                if (linePts.Count() > 1)
                    return this.GeometryFactory.CreateLineString(linePts);
            }
			return LineString.Empty;
		}

		public ILineString ReadLineString(Line line)
		{
			var coordinateList = new CoordinateList();
			coordinateList.Add(this.ReadCoordinate(line.StartPoint));
			coordinateList.Add(this.ReadCoordinate(line.EndPoint), this.AllowRepeatedCoordinates);
			if (coordinateList.Count > 1)
			{
				return this.GeometryFactory.CreateLineString(coordinateList.ToCoordinateArray());
			}
			return LineString.Empty;
		}

		public ILineString ReadLineString(Mline multiLine)
		{
			var coordinateList = new CoordinateList();
			int num = multiLine.NumberOfVertices - 1;
			for (int i = 0; i <= num; i++)
			{
				coordinateList.Add(this.ReadCoordinate(multiLine.VertexAt(i)), this.AllowRepeatedCoordinates);
			}
			if (multiLine.IsClosed)
			{
				coordinateList.Add(coordinateList[0]);
			}
			if (coordinateList.Count > 1)
			{
				return this.GeometryFactory.CreateLineString(coordinateList.ToCoordinateArray());
			}
			return LineString.Empty;
		}

		public ILineString ReadLineString(Arc arc)
		{
			var coordinateList = new CoordinateList(this.GetTessellatedCurveCoordinates(arc), this.AllowRepeatedCoordinates);
			if (coordinateList.Count > 1)
			{
				return this.GeometryFactory.CreateLineString(coordinateList.ToCoordinateArray());
			}
			return LineString.Empty;
		}

		public IMultiPolygon ReadMultiPolygon(MPolygon multiPolygon)
		{
			var list = new List<Polygon>();

			int num = multiPolygon.NumMPolygonLoops - 1;
			for (int i = 0; i <= num; i++)
			{
				if (multiPolygon.GetLoopDirection(i) == 0)
				{
                    var coordinates = this.GetMPolygonLoopCoordinates(multiPolygon, multiPolygon.GetMPolygonLoopAt(i));
                    var shell = (LinearRing)this.GeometryFactory.CreateLinearRing(coordinates);
                    var list2 = new List<LinearRing>();
					IntegerCollectionEnumerator enumerator = multiPolygon.GetChildLoops(i).GetEnumerator();
					while (enumerator.MoveNext())
					{
						int current = enumerator.Current;
                        if (multiPolygon.GetLoopDirection(current) == LoopDirection.Annotation) // 1????
						{
						    var coordinates1 = this.GetMPolygonLoopCoordinates(multiPolygon, multiPolygon.GetMPolygonLoopAt(current));
							list2.Add((LinearRing)this.GeometryFactory.CreateLinearRing(coordinates1));
						}
					}
					list.Add((Polygon)this.GeometryFactory.CreatePolygon(shell, list2.ToArray()));
				}
			}
			return this.GeometryFactory.CreateMultiPolygon(list.ToArray());
		}

        public IPolygon CreateCircle(double x, double y, double radius)
        {  
            int sides = 32;//圆上面的点个数  
            var coords = new Coordinate[sides + 1];
            for (int i = 0; i < sides; i++)
            {
                double angle = (i / (double)sides) * Math.PI * 2.0;
                double dx = Math.Cos(angle) * sides;
                double dy = Math.Sin(angle) * sides;  
                coords[i] = new Coordinate( x + dx, y + dy );  
            }
            coords[sides] = coords[0];  
            ILinearRing ring = GeometryFactory.CreateLinearRing(coords);  
            IPolygon polygon = GeometryFactory.CreatePolygon(ring, null);  
            return polygon;  
        }

        public IPolygon ReadCurveAsPolygon(Transaction tr, Curve curve)
        {
            Polyline polyline = curve as Polyline;
            if (polyline != null)
                return ReadPolygon(polyline);

            Polyline2d polyline2d = curve as Polyline2d;
            if (polyline2d != null)
                return ReadPolygon(polyline2d, tr);

            return null;
        }

        public int NumberOfVertices(Curve curve)
        {
            Polyline polyline = curve as Polyline;
            if (polyline != null)
            {
                return polyline.NumberOfVertices;
            }

            Polyline2d polyline2d = curve as Polyline2d;
            if (polyline2d != null)
            {
                int number = 0;
                foreach (var vertex in polyline2d)
                {
                    number++;
                }
                return number;
            }
            return -1;
        }

        public bool NumberOfVerticesMoreThan3(Curve curve)
        {
            Polyline polyline = curve as Polyline;
            if (polyline != null)
            {
                return polyline.NumberOfVertices > 3;
            }

            Polyline2d polyline2d = curve as Polyline2d;
            if (polyline2d != null)
            {
                int i = 0;
                foreach (var vertex in polyline2d)
                {
                    i++;
                    if (i > 3)
                        return true;
                }
            }
            return false;
        }

        public IPolygon ReadPolygon(Polyline polyline)
        {
            var coordinateList = ReadCoordination(polyline);
            if (coordinateList.Count > 1)
            {
                return this.GeometryFactory.CreatePolygon(coordinateList.ToCoordinateArray());
            }
            return Polygon.Empty;
        }

        public IPolygon ReadPolygon(Polyline2d polyline, Transaction tr)
        {
            var coordinateList = ReadCoordination(polyline, tr);
            if (coordinateList.Count > 1)
            {
                return this.GeometryFactory.CreatePolygon(coordinateList.ToCoordinateArray());
            }
            return Polygon.Empty;
        }

        CoordinateList ReadCoordination(Polyline polyline)
        {
            var coordinateList = new CoordinateList();
            int num = polyline.NumberOfVertices - 1;
            for (int i = 0; i <= num; i++)
            {
                SegmentType segmentType = polyline.GetSegmentType(i);
                if (segmentType == SegmentType.Arc)
                {
                    coordinateList.Add(this.GetTessellatedCurveCoordinates(polyline.GetArcSegmentAt(i)), this.AllowRepeatedCoordinates);
                }
                else
                {
                    coordinateList.Add(this.ReadCoordinate(polyline.GetPoint3dAt(i)), this.AllowRepeatedCoordinates);
                }
            }
            if (polyline.Closed)
            {
                coordinateList.Add(coordinateList[0]);
            }

            return coordinateList;
        }

        CoordinateList ReadCoordination(Polyline2d polyline2D, Transaction tr)
        {
            var coordinateList = new CoordinateList();
			if (polyline2D.PolyType == Poly2dType.SimplePoly)
			{
                foreach (var v in polyline2D)
                {
                    Vertex2d vertex = null;
                    if (v is ObjectId)
                    {
                        var id = (ObjectId)v;
                        if (id.IsValid)
                            vertex = tr.GetObject(id, OpenMode.ForRead) as Vertex2d;
                    }
                    else if (v is Vertex2d)
                    {
                        vertex = (Vertex2d)v;
                    }

                    if (vertex != null)
                        coordinateList.Add(this.ReadCoordinate(vertex.Position), this.AllowRepeatedCoordinates);
                }
			}
			else
			{
                var dBObjectCollection = new DBObjectCollection();
				polyline2D.Explode(dBObjectCollection);
				try
				{
                    foreach (var dBObject in dBObjectCollection)
				    {
				        if (dBObject is Arc)
				        {
                            var arc = (Arc)dBObject;
                            coordinateList.Add(this.GetTessellatedCurveCoordinates(arc), false);
				        }
                        else if (dBObject is Line)
                        {
                            var line = (Line)dBObject;
                            coordinateList.Add(this.ReadCoordinate(line.StartPoint), false);
                            coordinateList.Add(this.ReadCoordinate(line.EndPoint), false);
                        }
					}
				}
				finally
				{
                    foreach (var dBObject in dBObjectCollection)
                    {
                        if (dBObject is IDisposable)
                            (dBObject as IDisposable).Dispose();
                    }
				}
				dBObjectCollection.Dispose();
			}
			if (polyline2D.Closed)
			{
				coordinateList.Add(coordinateList[0]);
			}

            return coordinateList;
        }

        public IPolygon CreateCircle(Circle circle)
        {
            return CreateCircle(circle.Center.X, circle.Center.Y, circle.Radius);
        }

		public IGeometry ReadGeometry(Entity entity, Transaction tr)
		{
			string name = entity.GetRXClass().Name;
			if (name == "AcDbPoint")
			{
				return this.ReadPoint((DBPoint)entity);
			}
			if (name == "AcDbBlockReference")
			{
				return this.ReadPoint((BlockReference)entity);
			}
			if (name == "AcDbLine")
			{
				return this.ReadLineString((Line)entity);
			}
			if (name == "AcDbPolyline")
			{
				return this.ReadLineString((Polyline)entity);
			}
			if (name == "AcDb2dPolyline")
			{
				return this.ReadLineString(tr, (Polyline2d)entity);
			}
			if (name == "AcDb3dPolyline")
			{
                return this.ReadLineString(tr, (Polyline3d)entity);
			}
			if (name == "AcDbArc")
			{
				return this.ReadLineString((Arc)entity);
			}
			if (name == "AcDbMline")
			{
				return this.ReadLineString((Mline)entity);
			}
			if (name == "AcDbMPolygon")
			{
				return this.ReadMultiPolygon((MPolygon)entity);
			}
			throw new ArgumentException(string.Format("Conversion from {0} entity to IGeometry is not supported.", entity.GetRXClass().Name));
		}

		Coordinate[] GetTessellatedCurveCoordinates(CircularArc3d curve)
		{
			var coordinateList = new CoordinateList();

			if (curve.StartPoint != curve.EndPoint)
			{
				switch (this.CurveTessellationMethod)
				{
				case CurveTessellation.None:
					coordinateList.Add(this.ReadCoordinate(curve.StartPoint));
					coordinateList.Add(this.ReadCoordinate(curve.EndPoint));
					break;
				case CurveTessellation.Linear:
				{
					var samplePoints = curve.GetSamplePoints((int)Math.Round(this.CurveTessellationValue));
					for (int i = 0; i < samplePoints.Length; i++)
					{
						Point3d point3D = samplePoints[i].Point;
						coordinateList.Add(this.ReadCoordinate(point3D));
					}
					break;
				}
				case CurveTessellation.Scaled:
				{
					double num = curve.GetArea(curve.GetParameterOf(curve.StartPoint), curve.GetParameterOf(curve.EndPoint)) * this.CurveTessellationValue;
                    double num2 = Math.Acos((curve.Radius - 1.0 / (num / 2.0)) / curve.Radius);
					int num3 = (int)Math.Round(6.2831853071795862 / num2);
					if (num3 < 8)
					{
						num3 = 8;
					}
					if (num3 > 128)
					{
						num3 = 128;
					}

					var samplePoints2 = curve.GetSamplePoints(num3);
					for (int j = 0; j < samplePoints2.Length; j++)
					{
						var point3d2 = samplePoints2[j].Point;
						coordinateList.Add(this.ReadCoordinate(point3d2));
					}
					break;
				}
				}
			}
			return coordinateList.ToCoordinateArray();
		}

		Coordinate[] GetTessellatedCurveCoordinates(Matrix3d parentEcs, CircularArc2d curve)
		{
			Matrix3d matrix3d = parentEcs.Inverse();
			Point2d[] samplePoints = curve.GetSamplePoints(3);
			var point3d = new Point3d(samplePoints[0].X, samplePoints[0].Y, 0.0);
            var point3d2 = new Point3d(samplePoints[1].X, samplePoints[1].Y, 0.0);
            var point3d3 = new Point3d(samplePoints[2].X, samplePoints[2].Y, 0.0);
			point3d.TransformBy(matrix3d);
			point3d2.TransformBy(matrix3d);
			point3d3.TransformBy(matrix3d);
			return this.GetTessellatedCurveCoordinates(new CircularArc3d(point3d, point3d2, point3d3));
		}

		Coordinate[] GetTessellatedCurveCoordinates(Matrix3d parentEcs, Point2d startPoint, Point2d endPoint, double bulge)
		{
			return this.GetTessellatedCurveCoordinates(parentEcs, new CircularArc2d(startPoint, endPoint, bulge, false));
		}

		Coordinate[] GetTessellatedCurveCoordinates(Arc curve)
		{
			CircularArc3d curve2;
			try
			{
				curve2 = new CircularArc3d(curve.StartPoint, curve.GetPointAtParameter((curve.EndParam - curve.StartParam) / 2.0), curve.EndPoint);
			}
			catch (System.Exception ex)
			{
				//ProjectData.SetProjectError(expr_31);
                System.Diagnostics.Trace.WriteLine(ex);
				curve2 = new CircularArc3d(curve.StartPoint, curve.GetPointAtParameter((curve.EndParam + curve.StartParam) / 2.0), curve.EndPoint);
				//ProjectData.ClearProjectError();
			}
			return this.GetTessellatedCurveCoordinates(curve2);
		}

		Coordinate[] GetMPolygonLoopCoordinates(MPolygon multiPolygon, MPolygonLoop multiPolygonLoop)
		{
            var coordinateList = new CoordinateList();
			int num = multiPolygonLoop.Count - 1;
            for (var i = 0; i <= num; i++)
			{
				BulgeVertex bulgeVertex = multiPolygonLoop[i];
				if (Math.Abs(bulgeVertex.Bulge) < 1e-06)
				{
					coordinateList.Add(this.ReadCoordinate(bulgeVertex.Vertex), this.AllowRepeatedCoordinates);
				}
				else
				{
					Point2d vertex;
					if (i + 1 <= multiPolygonLoop.Count - 1)
					{
						vertex = multiPolygonLoop[i + 1].Vertex;
					}
					else
					{
						vertex = multiPolygonLoop[0].Vertex;
					}
					var tessellatedCurveCoordinates = this.GetTessellatedCurveCoordinates(multiPolygon.Ecs, bulgeVertex.Vertex, vertex, bulgeVertex.Bulge);
                    for (var j = 0; j < tessellatedCurveCoordinates.Length; j++)
					{
                        coordinateList.Add(tessellatedCurveCoordinates[j], this.AllowRepeatedCoordinates);
					}
				}
			}
			if (!coordinateList[0].Equals2D(coordinateList[coordinateList.Count - 1]))
			{
				coordinateList.Add(coordinateList[0]);
			}
			return coordinateList.ToCoordinateArray();
		}
    }
}
