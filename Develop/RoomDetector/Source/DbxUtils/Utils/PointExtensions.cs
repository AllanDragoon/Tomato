using System;
using Autodesk.AutoCAD.Geometry;

namespace DbxUtils.Utils
{
    public static class PointExtensions
    {
        public static Point2d Convert2dInXYPlane(this Point3d point3d)
        {
            return point3d.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis));
        }

        public static Point3d GetOrthoPoint(this Point3d basePt, Point3d pt)
        {
            // Apply a crude orthographic mode
            double x = pt.X;
            double y = pt.Y;

            Vector3d vec = basePt.GetVectorTo(pt);
            if (Math.Abs(vec.X) >= Math.Abs(vec.Y))
                y = basePt.Y;
            else
                x = basePt.X;

            return new Point3d(x, y, 0.0);
        }

        public static Vector3d ToVector3d(this Point3d point)
        {
            return new Vector3d(point.X, point.Y, point.Z);
        }
    }
}
