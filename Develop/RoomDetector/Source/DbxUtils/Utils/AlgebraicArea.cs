using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace DbxUtils.Utils
{
    /// <summary>
    /// How to know the orientation of a polyline 
    /// http://forums.autodesk.com/t5/NET/How-to-know-the-orientation-of-a-polyline/m-p/3784606/highlight/true#M33555
    /// </summary>
    public static class AlgebraicArea
    {
        public static double GetArea(Point2d pt1, Point2d pt2, Point2d pt3)
        {
            return (((pt2.X - pt1.X) * (pt3.Y - pt1.Y)) -
                        ((pt3.X - pt1.X) * (pt2.Y - pt1.Y))) / 2.0;
        }

        public static double GetArea(this CircularArc2d arc)
        {
            var rad = arc.Radius;
            var ang = arc.IsClockWise ?
                arc.StartAngle - arc.EndAngle :
                arc.EndAngle - arc.StartAngle;
            return rad * rad * (ang - Math.Sin(ang)) / 2.0;
        }

        public static double GetArea(this Polyline pline)
        {
            var area = 0.0;
            var last = pline.NumberOfVertices - 1;
            var p0 = pline.GetPoint2dAt(0);
            if (!pline.GetBulgeAt(0).EqualsWithTolerance(0.0))
            {
                area += pline.GetArcSegment2dAt(0).GetArea();
            }
            for (int i = 1; i < last; i++)
            {
                area += GetArea(p0, pline.GetPoint2dAt(i), pline.GetPoint2dAt(i + 1));
                if (!pline.GetBulgeAt(i).EqualsWithTolerance(0.0))
                {
                    area += pline.GetArcSegment2dAt(i).GetArea(); 
                }
            }
            if ((!pline.GetBulgeAt(last).EqualsWithTolerance(0.0)) && pline.Closed)
            {
                area += pline.GetArcSegment2dAt(last).GetArea();
            }
            return area;
        }

        //public static bool IsPolylineClockWise(this Polyline pline)
        //{
        //    if (pline == null) return true;
        //    var area = pline.GetArea();
        //    return area < 0;
        //}

        //public static bool IsPolylineClockWise(this Polyline2d pline, Transaction tr)
        //{
        //    if (pline == null) return true;

        //    bool result = false;
        //    //using (var tr = pline.Id.Database.TransactionManager.StartTransaction())
        //    //{
        //        Polyline tmpPolyline = new Polyline();
        //        switch (pline.PolyType)
        //        {
        //            case Poly2dType.SimplePoly:
        //            case Poly2dType.FitCurvePoly:
        //                tmpPolyline.ConvertFrom(pline, false);
        //                break;
        //            case Poly2dType.QuadSplinePoly:
        //            case Poly2dType.CubicSplinePoly:
        //                int index = 0;
        //                foreach (ObjectId vid in pline)
        //                {
        //                    Vertex2d v2d = tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
        //                    if (v2d.VertexType != Vertex2dType.SplineControlVertex)
        //                        tmpPolyline.AddVertexAt(index++, new Point2d(v2d.Position.X, v2d.Position.Y), 0, 0, 0);
        //                }
        //                break;
        //        }

        //        tmpPolyline.Closed = pline.Closed;
        //        result = IsPolylineClockWise(tmpPolyline);
        //    //}

        //    return result;

        //    ////////////////////////////////////////////////////////////////////////////////////////////////////////
        //    //// Willson: comments out this block which is checkig if the polyline 2d is clockwise,
        //    ////          as the comments below, we may encounter the issue if the first 3 vertext is in one line,
        //    ////          consider of the tolerance for different polyline may be diffent, so it seems difficult to
        //    ////          decide a satified tolerance value. So I decided to create a polyline and checking if the polyline
        //    ////          is clockwise. The new code logic need to be test more to verify if there is any issue.
        //    ///////////////////////////////////////////////////////////////////////////////////////////////////////
        //    //// Use foreach to get each contained vertex
        //    //using (var tr = pline.Database.TransactionManager.StartTransaction())
        //    //{
        //    //    Vertex2d p1 = null;
        //    //    Vertex2d p2 = null;
        //    //    Vertex2d p3 = null;
        //    //    int i = 0;

        //    //    // 取出前三个顶点，如果前三个顶点是顺时针，则为顺时针
        //    //    // 问题：如果多边形是直线这里的处理会有问题
        //    //    foreach (ObjectId vId in pline)
        //    //    {
        //    //        // http://through-the-interface.typepad.com/through_the_interface/2007/04/iterating_throu.html
        //    //        var vextex2D = (Vertex2d)tr.GetObject(vId, OpenMode.ForRead);
        //    //        if (i == 0)
        //    //            p1 = vextex2D;
        //    //        else if (i == 1)
        //    //            p2 = vextex2D;
        //    //        else if (i == 2)
        //    //        {
        //    //            p3 = vextex2D;
        //    //            break;
        //    //        }
        //    //        i++;
        //    //    }

        //    //    if (i == 2)
        //    //        return Clockwise(p1, p2, p3);
        //    //}
        //    //return false;
        //}

        //static bool Clockwise(Point2d p1, Point2d p2, Point2d p3)
        //{
        //    return ((p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X)) < 1e-9;
        //}

        //static bool Clockwise(Vertex2d p1, Vertex2d p2, Vertex2d p3)
        //{
        //    double value = ((p2.Position.X - p1.Position.X) * (p3.Position.Y - p1.Position.Y) -
        //        (p2.Position.Y - p1.Position.Y) * (p3.Position.X - p1.Position.X));
        //    return value < 1e-9;
        //}
    }
}
