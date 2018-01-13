using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Main
{
    public class WallRecognizer
    {
        public static int MILLIMETER_TO_METER = 1000;
        public static double PIXEL_TO_M_FACTOR = 136.70; // how many pixels indicate one Meter in canvas (=1/0.0254/0.288) where 0.288 is the PIXEL_TO_INCHES_FACTOR in flash version.
        public static double minWallWidth = 0.03 * MILLIMETER_TO_METER; // 30mm
        public static double maxWallWidth = 1 * MILLIMETER_TO_METER; //1000mm
        public static double width = maxWallWidth + 1;
        public static  Autodesk.AutoCAD.Geometry.Tolerance tol = new Autodesk.AutoCAD.Geometry.Tolerance(0.01, 0.01);

        public static Vector3d getLineDirection(Line line)
        {
            var dir = new Vector3d(line.EndPoint.X - line.StartPoint.X, line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.Y - line.StartPoint.Y);
            return dir.GetNormal();
        }
        public static LineSegment3d toLineSegment3d(Point3d start, Point3d end)
        {
            return new LineSegment3d(start, end);
        }
        public static Line3d toLine3d(Point3d start, Point3d end)
        {
            return new Line3d(start, end);
        }
        public static bool isSegmentsProjectionOverlapped(LineSegment3d lineseg1, LineSegment3d lineseg2)
        {
            Point3d projectPt1 = lineseg1.GetClosestPointTo(lineseg2.StartPoint).Point;
            Point3d projectPt2 = lineseg1.GetClosestPointTo(lineseg2.EndPoint).Point;
            // after the projection, the two line segments(p1->p2 & p3->p4) may be same
            if ((projectPt1.IsEqualTo(lineseg1.StartPoint, tol) && projectPt2.IsEqualTo(lineseg1.EndPoint, tol)) ||
                (projectPt1.IsEqualTo(lineseg1.EndPoint, tol) && projectPt2.IsEqualTo(lineseg1.StartPoint, tol)))
            {
                return true;
            }

            // [Daniel] retrun null if no overlap???
            LinearEntity3d overlap = lineseg1.Overlap(lineseg2);
            return (overlap != null);
        }

        public static LineSegment3d getWallline(Line line, IEnumerable<Line> entities)
        {
            // step 1: get the parallel lines in the loop
            var dir = WallRecognizer.getLineDirection(line);
            var parallelLines = new List<LineSegment3d>();
            foreach (Line tmpLine in entities)
            {
                if (line != tmpLine)
                {
                    var prepareDir = WallRecognizer.getLineDirection(tmpLine);
                    if (dir.IsParallelTo(prepareDir))
                        parallelLines.Add(WallRecognizer.toLineSegment3d(tmpLine.StartPoint, tmpLine.EndPoint));
                }
            }

            // step 2: [Daniel TODO: wall contains opening] check if colinear line
            var pt1InSegment = false;
            foreach (LineSegment3d tmplineSeg in parallelLines)
            {
                // to see if whether point in line segment.
                if (tmplineSeg.IsOn(line.StartPoint))
                {
                    pt1InSegment = true;
                    break;
                }
            }
            if (!pt1InSegment)
                return null;

            var pt2InSegment = false;
            foreach (LineSegment3d tmplineSeg in parallelLines)
            {
                // to see if whether point in line segment.
                if (tmplineSeg.IsOn(line.EndPoint))
                {
                    pt2InSegment = true;
                    break;
                }
            }
            if (!pt2InSegment)
                return null;

            LineSegment3d prepareLine = null;
            LineSegment3d lineSeg = WallRecognizer.toLineSegment3d(line.StartPoint, line.EndPoint);
            // step 3: get poper distance for wall width
            foreach (LineSegment3d lineSeg1 in parallelLines)
            {
                Line3d line3d = WallRecognizer.toLine3d(lineSeg1.StartPoint, lineSeg1.EndPoint); 
                // skip the colinear line and the two lines should be projection overlapped
                if (!line3d.IsOn(line.StartPoint) &&
                     WallRecognizer.isSegmentsProjectionOverlapped(lineSeg1, lineSeg))
                {
                    // to skip following case: the top lines may be openning (window) cross section
                    //    starPt ------ endPt
                    //           |    |
                    //    pt1-----    ------------pt2
                    //    |                        |
                    //    |                        |
                    //    |                        |
                    //    --------------------------
                    // get the related lines for the two points (pt1 or pt2)
                    var bHasOverlapped = false;
                    foreach (LineSegment3d lineSeg2 in parallelLines)
                    {
                        if (line3d.StartPoint.IsEqualTo(lineSeg2.StartPoint, tol) ||
                            line3d.StartPoint.IsEqualTo(lineSeg2.EndPoint, tol) ||
                            line3d.EndPoint.IsEqualTo(lineSeg2.StartPoint, tol) ||
                            line3d.EndPoint.IsEqualTo(lineSeg2.EndPoint, tol))
                        {
                            if (WallRecognizer.isSegmentsProjectionOverlapped(lineSeg1, lineSeg2))
                            {
                                bHasOverlapped = true;
                                break;
                            }
                        }
                    }

                    if (bHasOverlapped)
                    {
                        double dist = lineSeg.GetDistanceTo(line3d.StartPoint);
                        // pixel to millemeter
                        dist = (dist * MILLIMETER_TO_METER) / PIXEL_TO_M_FACTOR;
                        // the dist should > minWallWidth & < maxWallWidth
                        if (DoubleExtensions.Larger(dist, minWallWidth) &&
                            DoubleExtensions.Larger(maxWallWidth, dist) &&
                            DoubleExtensions.Larger(width, dist))
                        {
                            width = Math.Round(dist);
                            prepareLine = lineSeg;
                            //var perpendicular = hsw.util.Math.getPerpendicularIntersect(pt1, starPt, endPt);
                            //prepareLine = new goog.math.Line(pt1.x, pt1.y, perpendicular.x, perpendicular.y);
                        }
                    }
                }
            }

            if (DoubleExtensions.LargerOrEqual(width, minWallWidth) && 
                DoubleExtensions.LargerOrEqual(maxWallWidth, width)) {
                return  prepareLine;
            }

            return null;
        }
    }
}
