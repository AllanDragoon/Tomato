using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
//using LS.MapClean.Addin.Algorithms;
//using LS.MapClean.Addin.Utils;
using System;
using System.Linq;
using Exception = System.Exception;

namespace DbxUtils.Utils
{
    public class PolylineUtils1
    {
        public static double GetParcelArea(ObjectId objectId)
        {
            double area;
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)transaction.GetObject(objectId, OpenMode.ForRead);
                area = GetParcelArea(entity);
                transaction.Commit();
            }
            if (double.IsNaN(area))
                return 0;
            return area;
        }

        public static double GetParcelArea(Entity polyline)
        {
            var parcelPolyline = polyline as Curve;
            if (parcelPolyline != null)
            {
                return parcelPolyline.Area;
            }
            throw new Exception("Wrong parcel entity");
        }

        public static double GetParcelLength(ObjectId objectId)
        {
            double length;
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)transaction.GetObject(objectId, OpenMode.ForRead);
                length = GetParcelLength(entity);
                transaction.Commit();
            }

            if (double.IsNaN(length))
                return 0;
            return length;
        }

        public static double GetParcelLength(Entity parcel)
        {
            var parcelPolyline = parcel as Polyline;
            if (parcelPolyline != null)
            {
                return parcelPolyline.Length;
            }
            var parcelPolyline2D = parcel as Polyline2d;
            if (parcelPolyline2D != null)
            {
                return parcelPolyline2D.Length;
            }
            return 0;
        }

        public static bool IsPolyline(ObjectId objectId)
        {
            using (var tr = objectId.Database.TransactionManager.StartTransaction())
            {
                DBObject dbObject = tr.GetObject(objectId, OpenMode.ForRead);
                tr.Commit();
                return dbObject is Polyline;
            }
        }

        public static Point3dCollection GetBoundaryPointCollection(Transaction transaction, DBObject dbObject, bool allowDuplicate = false)
        {
            var boundaryInModelSpace = new Point3dCollection();
            var polyline = dbObject as Polyline;
            if (polyline != null)
            {
                var numberOfVertices = polyline.NumberOfVertices;
                for (var i = 0; i < numberOfVertices; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    boundaryInModelSpace.Add(new Point3d(vertex.X, vertex.Y, 0));
                }
            }

            var polyline2D = dbObject as Polyline2d;
            if (polyline2D != null)
            {
                //var vertexIds = new ObjectIdCollection();
                //// Use foreach to get each contained vertex
                //foreach (ObjectId vertexId in polyline2D)
                //{
                //    vertexIds.Add(vertexId);
                //    var vertex = (Vertex2d) transaction.GetObject(vertexId, OpenMode.ForRead);
                //    boundaryInModelSpace.Add(new Point3d(vertex.Position.X, vertex.Position.Y, 0));
                //}

                // vertexId has a 

                var vertexIds = new ObjectIdCollection();
                // Use foreach to get each contained vertex
                foreach (ObjectId vertexId in polyline2D)
                {
                    vertexIds.Add(vertexId);
                    var vertex = (Vertex2d) transaction.GetObject(vertexId, OpenMode.ForRead);
                    boundaryInModelSpace.Add(new Point3d(vertex.Position.X, vertex.Position.Y, 0));
                }
            }

            if (!allowDuplicate)
            {
                RemoveDuplicatePointFromPoint3DCollection(boundaryInModelSpace);
            }

            return boundaryInModelSpace;
        }

        public static Point3dCollection GetNonRepeatBoundaryPointCollection(Transaction tr, DBObject dbObject)
        {
            var boundaryPoints = GetBoundaryPointCollection(tr, dbObject);
            RemoveDuplicatePointFromPoint3DCollection(boundaryPoints);
            return boundaryPoints;
        }

        public static Point3dCollection GetBoundaryPointCollection(ObjectId objectId, bool allowDuplicate = false)
        {
            Point3dCollection boundaryInModelSpace;
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                using (DBObject dbObject = transaction.GetObject(objectId, OpenMode.ForRead))
                {
                    boundaryInModelSpace = GetBoundaryPointCollection(transaction, dbObject);

                    // 允许重复
                    if (!allowDuplicate)
                    {
                        RemoveDuplicatePointFromPoint3DCollection(boundaryInModelSpace);
                    }
                }

                transaction.Commit();
            }

            return boundaryInModelSpace;
        }

        public static void RemoveDuplicatePointFromPoint3DCollection(Point3dCollection source)
        {
            var result = new Point3dCollection();
            foreach (Point3d point3D in source)
            {
                // Check duplicate
                if (DetectDuplicatePoint(result, point3D))
                    continue;
                result.Add(point3D);
            }

            // 如果个数不等的话：
            if (result.Count != source.Count)
            {
                source.Clear();
                foreach (Point3d point3D in result)
                    source.Add(point3D);
            }
        }

        public static Point3dCollection RemoveDuplicatePoint(Point3dCollection source)
        {
            var result = new Point3dCollection();
            foreach (Point3d point3D in source)
            {
                // Check duplicate
                if (DetectDuplicatePoint(result, point3D))
                    continue;

                result.Add(point3D);
            }

            return result;
        }

        public static bool DetectDuplicatePoint(Point3dCollection points, Point2d point)
        {
            return points.Cast<Point3d>().Any(existing => DetectDuplicatePoint(existing, point));
        }

        public static bool DetectDuplicatePoint(Point3dCollection points, Point3d point)
        {
            return points.Cast<Point3d>().Any(existing => DetectDuplicatePoint(existing, point));
        }

        public static bool DetectDuplicatePoint(Point3d existing, Point2d point)
        {
            double dist = Math.Sqrt((existing.X - point.X) * (existing.X - point.X)
                       + (existing.Y - point.Y) * (existing.Y - point.Y));
            return Math.Abs(dist) < 1e-06;
        }

        public static bool DetectDuplicatePoint(Point3d existing, Point3d point)
        {
            double dist = Math.Sqrt((existing.X - point.X) * (existing.X - point.X)
                       + (existing.Y - point.Y) * (existing.Y - point.Y));
            return Math.Abs(dist) < 1e-06;
        }

        public static bool DetectDuplicatePoint(Vertex2d existing, Point2d point)
        {
            double dist = Math.Sqrt((existing.Position.X - point.X) * (existing.Position.X - point.X)
                       + (existing.Position.Y - point.Y) * (existing.Position.Y - point.Y));
            return Math.Abs(dist) < 1e-06;
        }

        public static ObjectId DetectVertextObjectId(ObjectId polyline2DId, double x, double y)
        {
            return DetectVertextObjectId(polyline2DId, new Point2d(x, y));
        }

        public static ObjectId DetectVertextObjectId(ObjectId polyline2DId, Point3d point3D)
        {
            return DetectVertextObjectId(polyline2DId, new Point2d(point3D.X, point3D.Y));
        }

        public static ObjectId DetectVertextObjectId(ObjectId polyline2DId, Point2d point)
        {
            using (var transaction = polyline2DId.Database.TransactionManager.StartTransaction())
            {
                DBObject dbObject = transaction.GetObject(polyline2DId, OpenMode.ForRead);
                var polyline2D = dbObject as Polyline2d;
                if (polyline2D != null)
                {
                    var vertexIds = new ObjectIdCollection();
                    // Use foreach to get each contained vertex
                    foreach (ObjectId vertexId in polyline2D)
                    {
                        vertexIds.Add(vertexId);
                        var vertex = (Vertex2d)transaction.GetObject(vertexId, OpenMode.ForRead);

                        // Check duplicate
                        if (DetectDuplicatePoint(vertex, point))
                            return vertexId;
                    }
                }
            }

            return ObjectId.Null;
        }

        public static bool DetectDuplicatePoint(Point3dCollection points, Vertex2d point)
        {
            foreach (Point3d existing in points)
            {
                double dist = Math.Sqrt((existing.X - point.Position.X) * (existing.X - point.Position.X)
                        + (existing.Y - point.Position.Y) * (existing.Y - point.Position.Y));
                if (Math.Abs(dist) < 1e-06)
                    return true;
            }
            return false;
        }
    }
}
