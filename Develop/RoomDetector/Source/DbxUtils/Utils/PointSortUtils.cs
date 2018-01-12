using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DbxUtils.Utils
{
    class Sort2DbyX : IComparer<Point2d>
    {
        public static bool IsZero(double a)
        {
            return Math.Abs(a) < Tolerance.Global.EqualPoint;
        }

        public static bool IsEqual(double a, double b)
        {
            return IsZero(b - a);
        }

        public int Compare(Point2d a, Point2d b)
        {
            if (IsEqual(a.X, b.X)) return 0; // ==
            if (a.X < b.X) return -1; // <
            return 1; // >
        }
    }

    internal class Sort3DbyX : IComparer<Point3d>
    {
        public static bool IsZero(double a)
        {
            return Math.Abs(a) < Tolerance.Global.EqualPoint;
        }

        public static bool IsEqual(double a, double b)
        {
            return IsZero(b - a);
        }

        public int Compare(Point3d a, Point3d b)
        {
            if (IsEqual(a.X, b.X)) return 0; // ==
            if (a.X < b.X) return -1; // <
            return 1; // >
        }
    }

    internal class Sort3DByCurveParam : IComparer<Point3d>
    {
        private Curve _curve;
        public Sort3DByCurveParam(Curve curve)
        {
            _curve = curve;
        }

        public static bool IsZero(double a)
        {
            return Math.Abs(a) < Tolerance.Global.EqualPoint;
        }

        public static bool IsEqual(double a, double b)
        {
            return IsZero(b - a);
        }

        public int Compare(Point3d a, Point3d b)
        {
            // In some circumstances, point a or point b is not on the curve. So we near to "project" to point to the curve and them compare.

            double paramA = 0.0;
            double paramB = 0.0;
            try
            {
                paramA = _curve.GetParameterAtPoint(a);
                paramB = _curve.GetParameterAtPoint(b);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                Point3d a1 = _curve.GetClosestPointTo(a, true);
                Point3d b1 = _curve.GetClosestPointTo(b, true);

                paramA = _curve.GetParameterAtPoint(a1);
                paramB = _curve.GetParameterAtPoint(b1);
            }

            if (IsEqual(paramA, paramB)) return 0; // ==
            if (paramA < paramB) return -1; // <
            return 1; // >
        }
    }


    // Sorting an AutoCAD Point2dCollection or Point3dCollection using .NET
    //
    // http://through-the-interface.typepad.com/through_the_interface/2011/01/sorting-an-autocad-point2dcollection-or-point3dcollection-using-net.html
    // When working with the Point2d and Point3d objects from AutoCAD¡¯s .NET API it¡¯s common to use the in-built collection types, 
    // Point2dCollection and Point3dCollection. Neither of these objects provides the capability to sort their contents
    // (and, in any case, sorting would mean different things to different people when it comes to containers of objects with multiple data entries).
    // So how can you sort one of these collections?
    // The answer is fairly straightforward: you convert them to a .NET array, sort them using standard .NET capabilities and then recreate an AutoCAD collection.
    public static class PointSortUtils
    {
        public static Point2dCollection SortPoint2D(Point2dCollection point2Ds)
        {
            var raw = point2Ds.ToArray();
            Array.Sort(raw, new Sort2DbyX());
            return new Point2dCollection(raw);
        }

        public static Point3dCollection SortPoint3D(Point3dCollection point3Ds)
        {
            var raw3D = new Point3d[point3Ds.Count];
            point3Ds.CopyTo(raw3D, 0);
            Array.Sort(raw3D, new Sort3DbyX());
            return new Point3dCollection(raw3D);
        }

        public static Point3dCollection SortPoint3DByCurveParam(Curve curve, Point3dCollection point3Ds)
        {
            var raw3D = new Point3d[point3Ds.Count];
            point3Ds.CopyTo(raw3D, 0);
            Array.Sort(raw3D, new Sort3DByCurveParam(curve));
            return new Point3dCollection(raw3D);
        }
    }
}
