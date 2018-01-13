using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Utils
{
    public class ComputerGraphics
    {
        #region Check whether a point is inside a polygon
        // Algorithm from http://forums.autodesk.com/t5/net/point-on-polygon/td-p/4347893
        // wn_PnPoly(): winding number test for a point in a polygon
        //      Input:   P = a point,
        //               V[] = vertex points of a polygon V[n+1] with V[n]=V[0]
        //      Return:  wn = the winding number (=0 only if P is outside V[])
        public static bool IsInPolygon(Point2d[] V, Point2d P, int n)
        {
            int wn = 0;    // the winding number counter

            // loop through all edges of the polygon
            for (int i = 0; i < n; i++)
            {   // edge from V[i] to V[i+1]
                if (V[i].Y.SmallerOrEqual(P.Y)) // <= P.Y
                {         // start y <= P.Y
                    if (V[i + 1].Y.Larger(P.Y))      // > P.Y, an upward crossing
                        if (IsLeft(V[i], V[i + 1], P).Larger(0))  // > 0, P left of edge
                            ++wn;            // have a valid up intersect
                }
                else
                {                       // start y > P.Y (no test needed)
                    if (V[i + 1].Y.SmallerOrEqual(P.Y))    // <= P.Y, a downward crossing
                        if (IsLeft(V[i], V[i + 1], P).Smaller(0))  // < 0, P right of edge
                            --wn;            // have a valid down intersect
                }
            }

            if (wn == 0)
                return false;
            else
                return true;
        }

        // isLeft(): tests if a point is Left|On|Right of an infinite line.
        //    Input:  three points P0, P1, and P2
        //    Return: >0 for P2 left of the line through P0 and P1
        //            =0 for P2 on the line
        //            <0 for P2 right of the line
        //    See: the January 2001 Algorithm "Area of 2D and 3D Triangles and Polygons"
        public static double IsLeft(Point2d P0, Point2d P1, Point2d P2)
        {
            return ((P1.X - P0.X) * (P2.Y - P0.Y) - (P2.X - P0.X) * (P1.Y - P0.Y));
        }
        #endregion

        #region Check whether a polygon is CLOCKWISE or COUNTERCLOCKWISE
        /// <summary>
        /// http://debian.fmi.uni-sofia.bg/~sergei/cgsr/docs/clockwise.htm
        /// Return the clockwise status of a curve, clockwise or counterclockwise
        /// n vertices making up curve p
        /// return 0 for incomputables eg: colinear points
        /// CLOCKWISE == 1
        /// COUNTERCLOCKWISE == -1
        /// It is assumed that
        /// - the polygon is closed
        /// - the last point is not repeated.
        /// - the polygon is simple (does not intersect itself or have holes)
        /// </summary>
        /// <returns></returns>
        //public static int ClockWise(Point2d[] p)
        //{
        //    const int COUNTERCLOCKWISE = -1;
        //    const int CLOCKWISE = 1;

        //    int i, j, k;
        //    int count = 0;
        //    double z;

        //    int n = p.Length;
        //    if (n < 3)
        //        return (0);

        //    for (i = 0; i < n; i++)
        //    {
        //        j = (i + 1) % n;
        //        k = (i + 2) % n;
        //        z = (p[j].X - p[i].X) * (p[k].Y - p[j].Y);
        //        z -= (p[j].Y - p[i].Y) * (p[k].X - p[j].X);
        //        if (z < 0)
        //            count--;
        //        else if (z > 0)
        //            count++;
        //    }
        //    if (count > 0)
        //        return (COUNTERCLOCKWISE);
        //    else if (count < 0)
        //        return (CLOCKWISE);
        //    else
        //        return (0);
        //}

        /// <summary>
        /// http://dominoc925.blogspot.com/2012/03/c-code-to-determine-if-polygon-vertices.html
        /// The above ClockWise method returns 0 in some cases, so I add this method here
        /// It is assumed that
        /// - the polygon is closed
        /// - the last point is equal to the first one.
        /// - the polygon is simple (does not intersect itself or have holes)
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        public static bool ClockWise2(Point2d[] polygon)
        {
            bool isClockwise = false;
            double sum = 0;
            for (int i = 0; i < polygon.Length - 1; i++)
            {
                sum += (polygon[i + 1].X - polygon[i].X) * (polygon[i + 1].Y + polygon[i].Y);
            }
            isClockwise = (sum > 0) ? true : false;
            return isClockwise;
        }
        #endregion

        #region Check whether a polygon is convex or concave
        /// <summary>
        /// http://debian.fmi.uni-sofia.bg/~sergei/cgsr/docs/clockwise.htm
        /// Return whether a polygon in 2D is concave or convex
        /// return 0 for incomputables eg: colinear points
        ///  CONVEX == 1
        ///  CONCAVE == -1
        /// It is assumed that the polygon is simple
        /// (does not intersect itself or have holes)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static int Convex(Point2d[] p)
        {
            const int CONVEX = 1;
            const int CONCAVE = -1;

            int i, j, k;
            int flag = 0;
            double z;

            int n = p.Length;
            if (n < 3)
                return (0);

            for (i = 0; i < n; i++)
            {
                j = (i + 1) % n;
                k = (i + 2) % n;
                z = (p[j].X - p[i].X) * (p[k].Y - p[j].Y);
                z -= (p[j].Y - p[i].Y) * (p[k].X - p[j].X);
                if (z < 0)
                    flag |= 1;
                else if (z > 0)
                    flag |= 2;
                if (flag == 3)
                    return (CONCAVE);
            }
            if (flag != 0)
                return (CONVEX);
            else
                return (0);
        }
        #endregion

        #region Polygon Area
        /// <summary>
        /// http://mathopenref.com/coordpolygonarea2.html
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double PolygonArea(Point3d[] points)
        {
            var area = 0d;         // Accumulates area in the loop
            var j = points.Length - 1;  // The last vertex is the 'previous' one to the first

            for (var i = 0; i < points.Length; i++)
            {
                var previous = points[j];
                var point = points[i];
                area = area + (previous.X + point.X) * (previous.Y - point.Y);
                j = i;  //j is previous vertex to i
            }
            return Math.Abs(area / 2);
        }
        #endregion
    }
}
