using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Utils
{
    public class GeometryUtils
    {
        private const double AreaTolerance = 0.0001;
        private const double DistanceTolerance = 0.0001;

        public static List<Polyline> RegionToPolylines(Database db, Transaction tr, Region reg, 
            int colorIndex, bool willAddToModelSpace, bool needToConvertPolyline2d, ObjectId layerId)
        {
            var color = ColorList[colorIndex % (ColorList.Length)];
            return RegionToPolylines(db, tr, reg, color, willAddToModelSpace, needToConvertPolyline2d, layerId);
        }

        public static List<Polyline> RegionToPolylines(Database db, Transaction tr, Region reg, 
            Color color, bool willAddToModelSpace, bool needToConvertPolyline2d, ObjectId layerId)
        {
            var polylines = new List<Polyline>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            if (reg != null && reg.IsNull == false)
            {
                // The resulting Polyline
                var p = new Polyline();

                // Explode Region -> collection of Curves
                var cvs = new DBObjectCollection();
                reg.Explode(cvs);

                // Create a plane to convert 3D coords
                // into Region coord system

                var pl = new Plane(new Point3d(0, 0, 0), reg.Normal);

                // Set common entity properties from the Region

                p.SetPropertiesFrom(reg);

                //check if exploded region is still a region. If yes, We also need to explode the childe region.
                bool hasRegion = false;
                foreach (DBObject obj in cvs)
                {
                    var region = obj as Region;
                    if (region != null)
                    {
                        hasRegion = true;
                        var subPolylines = RegionToPolylines(db, tr, region, color, willAddToModelSpace, needToConvertPolyline2d, layerId);
                        region.Dispose();
                        polylines.AddRange(subPolylines);
                    }
                }
                if (hasRegion)
                {
                    cvs.Dispose();
                    pl.Dispose();
                    return polylines;
                }

                // For initial Curve take the first in the list

                var cv1 = cvs[0] as Curve;

                p.AddVertexAt(
                  p.NumberOfVertices,
                  cv1.StartPoint.Convert2d(pl),
                  BulgeFromCurve(cv1, false), 0, 0
                );

                p.AddVertexAt(
                  p.NumberOfVertices,
                  cv1.EndPoint.Convert2d(pl),
                  0, 0, 0
                );

                cvs.Remove(cv1);

                // The next point to look for

                Point3d nextPt = cv1.EndPoint;

                cv1.Dispose();

                // Find the line that is connected to
                // the next point

                // If for some reason the lines returned were not
                // connected, we could loop endlessly.
                // So we store the previous curve count and assume
                // that if this count has not been decreased by
                // looping completely through the segments once,
                // then we should not continue to loop.
                // Hopefully this will never happen, as the curves
                // should form a closed loop, but anyway...

                // Set the previous count as artificially high,
                // so that we loop once, at least.

                int prevCnt = cvs.Count + 1;
                while (cvs.Count > 0 && cvs.Count < prevCnt)
                {
                    prevCnt = cvs.Count;
                    foreach (Curve cv in cvs)
                    {
                        // If one end of the curve connects with the
                        // point we're looking for...

                        if (cv.StartPoint == nextPt ||
                            cv.EndPoint == nextPt)
                        {
                            // Calculate the bulge for the curve and
                            // set it on the previous vertex

                            double bulge = BulgeFromCurve(cv, cv.EndPoint == nextPt);
                            p.SetBulgeAt(p.NumberOfVertices - 1, bulge);

                            // Reverse the points, if needed

                            if (cv.StartPoint == nextPt)
                                nextPt = cv.EndPoint;
                            else
                                // cv.EndPoint == nextPt
                                nextPt = cv.StartPoint;

                            // Add out new vertex (bulge will be set next
                            // time through, as needed)

                            p.AddVertexAt(
                              p.NumberOfVertices,
                              nextPt.Convert2d(pl),
                              0, 0, 0
                            );

                            // Remove our curve from the list, which
                            // decrements the count, of course

                            cvs.Remove(cv);
                            cv.Dispose();
                            break;
                        }
                    }
                }
                if (cvs.Count >= prevCnt)
                {
                    // Error.
                    p.Dispose();
                }
                else
                {
                    // Once we have added all the Polyline's vertices,
                    // transform it to the original region's plane

                    p.TransformBy(Matrix3d.PlaneToWorld(pl));
                    if (layerId.IsValid)
                        p.LayerId = layerId;
                    p.Color = color;

                    // Extra checking if the polyline end point is same as start point. If yes, remove the end point.
                    var startPoint2d = p.GetPoint2dAt(0);
                    var endPoint2d = p.GetPoint2dAt(p.NumberOfVertices - 1);
                    if (Math.Abs(startPoint2d.X - endPoint2d.X) < DistanceTolerance && Math.Abs(startPoint2d.Y - endPoint2d.Y) < DistanceTolerance)
                    {
                        double lastBulge = p.GetBulgeAt(p.NumberOfVertices - 2);
                        p.RemoveVertexAt(p.NumberOfVertices - 1);
                        p.SetBulgeAt(p.NumberOfVertices - 1, lastBulge);
                    }

                    // Make sure the polyline is closed.
                    if (!p.Closed)
                        p.Closed = true;

                    // 设置标高.
                    p.Elevation = 0.0;

                    if (p.Area < AreaTolerance)
                    {
                        p.Dispose();
                        return polylines;
                    }

                    // Append our new Polyline to the database
                    if (willAddToModelSpace)
                    {
                        btr.UpgradeOpen();
                        btr.AppendEntity(p);
                        tr.AddNewlyCreatedDBObject(p, true);

                        //// 确保生成出来的polyline为顺时针方向
                        //GeometryUtils.EnsurePolylineClockWise(p.ObjectId);
                    }
                    pl.Dispose();
                    polylines.Add(p);
                    return polylines;
                }

                pl.Dispose();
            }

            return polylines;
        }

        public static double BulgeFromCurve(Curve cv, bool clockwise)
        {
            double bulge = 0.0;

            Arc a = cv as Arc;
            if (a != null)
            {
                double newStart;

                // The start angle is usually greater than the end,
                // as arcs are all counter-clockwise.
                // (If it isn't it's because the arc crosses the
                // 0-degree line, and we can subtract 2PI from the
                // start angle.)

                if (a.StartAngle > a.EndAngle)
                    newStart = a.StartAngle - 8 * Math.Atan(1);
                else
                    newStart = a.StartAngle;

                // Bulge is defined as the tan of
                // one fourth of the included angle

                bulge = Math.Tan((a.EndAngle - newStart) / 4);

                // If the curve is clockwise, we negate the bulge

                if (clockwise)
                    bulge = -bulge;
            }
            return bulge;
        }

        public static Color[] ColorList = {Color.FromRgb(255, 0, 0),     // Red
                                            Color.FromRgb(255, 255, 0),   // Yellow
                                            Color.FromRgb(0, 0, 255),     // Blue
                                            Color.FromRgb(0, 255, 0),     // Green
                                            Color.FromRgb(163, 73, 164),  // Purple
                                            Color.FromRgb(255, 128, 0),   // Orange
                                            Color.FromRgb(0, 255, 255)    // Cyan
                                           };

        /// <summary>
        /// http://through-the-interface.typepad.com/through_the_interface/2012/01/testing-whether-a-point-is-on-any-autocad-curve-using-net.html
        /// A generalised IsPointOnCurve function that works on all
        /// types of Curve (including Polylines), and checks the position
        /// of the returned point rather than relying on catching an exception
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static bool IsPointOnCurveGCP(Curve cv, Point3d pt)
        {
            try
            {
                // Return true if operation succeeds
                Point3d p = cv.GetClosestPointTo(pt, false);
                return (p - pt).Length <= Tolerance.Global.EqualPoint;
            }
            catch { }

            // Otherwise we return false
            return false;
        }

        public static double? GetPointParameter(Curve curve, Point3d? point)
        {
            if (point == null)
                return null;

            Point3d closestPoint = curve.GetClosestPointTo(point.Value, true);
            double param = curve.GetParameterAtPoint(closestPoint);

            return param;
        }

        /// <summary>
        /// http://adndevblog.typepad.com/autocad/2012/05/how-to-detect-if-a-polyline-is-self-intersecting.html
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns></returns>
        public static bool IsPoygonSelfIntersect(Polyline polygon)
        {
            // Convert to a polyline with distinct vertices.
            var polyline = ConvertToDistinctVerticesPolygon(polygon);

            int edgeCount = polyline.NumberOfVertices - 1;
            for (int i = 0; i < edgeCount; ++i)
            {
                for (int j = i + 1; j < edgeCount; ++j)
                {
                    var line1 = polyline.GetLineSegmentAt(i);
                    var line2 = polyline.GetLineSegmentAt(j);

                    Point3d[] points = line1.IntersectWith(line2);
                    if (points == null)
                    {
                        continue;
                    }

                    foreach (Point3d point in points)
                    {
                        // Make a check to skip the start/end points
                        // since they are connected vertices
                        if (point == line1.StartPoint ||
                            point == line1.EndPoint)
                        {
                            if (point == line2.StartPoint ||
                                point == line2.EndPoint)
                            {
                                // If two consecutive segments, then skip
                                if (j == i + 1 || j == i - 1 || (i == 0 && j == edgeCount - 1))
                                {
                                    continue;
                                }
                            }
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Get distincet vertices of polyline.
        /// Sometimes a polyline has duplicate vertices.
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns></returns>
        private static List<Point2d> GetDistinctVertices(Polyline polyline)
        {
            var distinctVertices = new List<Point2d>();

            var prevVertex = polyline.GetPoint2dAt(0);
            distinctVertices.Add(prevVertex);

            for (int i = 1; i < polyline.NumberOfVertices; i++)
            {
                var currentVertex = polyline.GetPoint2dAt(i);
                if (currentVertex != prevVertex)
                {
                    distinctVertices.Add(currentVertex);
                    prevVertex = currentVertex;
                }
            }

            return distinctVertices;
        }
        private static Polyline ConvertToDistinctVerticesPolygon(Polyline polyline)
        {
            var distinctVertices = GetDistinctVertices(polyline);

            if (distinctVertices.Count == polyline.NumberOfVertices)
                return polyline;

            var result = new Polyline(distinctVertices.Count);
            for (int i = 0; i < distinctVertices.Count; i++)
            {
                result.AddVertexAt(i, distinctVertices[i], 0, 0, 0);
            }
            result.Closed = polyline.Closed;

            return result;
        }

        #region Extents
        public static Point3d GetCenterPoint(Extents3d ext)
        {
            return ext.MinPoint + ((ext.MaxPoint - ext.MinPoint) / 2.0);
        }

        public static Extents3d EnlargeExtend(Extents3d ext, double scaleRatio)
        {
            Extents3d tmpExt = new Extents3d(ext.MinPoint, ext.MaxPoint);
            Matrix3d scalingMat = Matrix3d.Scaling(scaleRatio, GetCenterPoint(tmpExt));
            tmpExt.TransformBy(scalingMat);
            return tmpExt;
        }

        public static Extents3d? SafeGetGeometricExtents(ObjectId entityId)
        {
            using (var transaction = entityId.Database.TransactionManager.StartTransaction())
            {
                var entity = transaction.GetObject(entityId, OpenMode.ForRead) as Entity;
                Extents3d? ext = SafeGetGeometricExtents(entity);
                transaction.Commit();
                return ext;
            }
        }

        public static Extents3d? SafeGetGeometricExtents(Entity entity)
        {
            // http://adndevblog.typepad.com/autocad/2012/12/entitygeometricextents-throws-an-exception-enullextents.html
            // Issue
            // When I calculate the extents of entities in a drawing, for some entities an exception is thrown with the "eNullExtents" message. 
            // What is wrong?
            // Solution
            // This exception occurs for an insert of an empty block or for an empty block attribute. 
            // It is "as designed", it's just a notification to the developer about an empty object. 
            // An easy solution is to add a separate catch block for this particular exception:
            var extents = new Extents3d();
            try
            {
                extents = entity.GeometricExtents;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                // The entity is empty and has no extents
                if (ex.Message == "eNullExtents" || ex.Message == "eInvalidExtents")
                {
                    // TODO. We can simply skip this entity...
                    return null;
                }
            }

            return extents;
        }

        public static Extents3d SafeGetGeometricExtents(IEnumerable<ObjectId> entityIds)
        {
            var idCollection = ToObjectIdCollection(entityIds);
            return SafeGetGeometricExtents(idCollection);
        }

        public static Extents3d SafeGetGeometricExtents(ObjectIdCollection entityIds)
        {
            var result = new Extents3d(new Point3d(0, 0, 0), new Point3d(1, 1, 1));
            using (var transaction = entityIds[0].Database.TransactionManager.StartTransaction())
            {
                var first = true;
                foreach (ObjectId objId in entityIds)
                {
                    if (!objId.IsValid)
                        continue;

                    var entity = transaction.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        // 如果有图形几何是NullExtent，直接skip掉
                        var extents = SafeGetGeometricExtents(entity);
                        if (extents == null)
                            continue;

                        if (first)
                        {
                            result = extents.Value;
                            first = false;
                        }
                        else
                        {
                            result.AddExtents(extents.Value);
                        }
                    }
                }
            }

            return result;
        }

        public static ObjectIdCollection ToObjectIdCollection(IEnumerable<ObjectId> objectIds)
        {
            var dbObjIdCollection = new ObjectIdCollection();
            foreach (ObjectId objectId in objectIds)
            {
                dbObjIdCollection.Add(objectId);
            }
            return dbObjIdCollection;
        }
        #endregion
    }
}
