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
    public class WallInfor {
        public LineSegment3d outline;
        public List<LineSegment3d> innerlines;
        public WallInfor next = null;
    }
    public class WallRecognizer
    {
        public static int MILLIMETER_TO_METER = 1000;
        public static double PIXEL_TO_M_FACTOR = 136.70; // how many pixels indicate one Meter in canvas (=1/0.0254/0.288) where 0.288 is the PIXEL_TO_INCHES_FACTOR in flash version.
        public static double minWallWidth = 0.03 * MILLIMETER_TO_METER; // 30mm
        public static double maxWallWidth = 1 * MILLIMETER_TO_METER; //1000mm
        //public static  Autodesk.AutoCAD.Geometry.Tolerance tol = new Autodesk.AutoCAD.Geometry.Tolerance(0.01, 0.01);

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
            if ((projectPt1.IsEqualTo(lineseg1.StartPoint) && projectPt2.IsEqualTo(lineseg1.EndPoint)) ||
                (projectPt1.IsEqualTo(lineseg1.EndPoint) && projectPt2.IsEqualTo(lineseg1.StartPoint)))
            {
                return true;
            }

            // [Daniel] retrun null if no overlap???
            LinearEntity3d overlap = lineseg1.Overlap(lineseg2);
            return (overlap != null);
        }

        public static WallInfor getWallinfors(List<LineSegment3d> outLines, List<LineSegment3d> allLines)
        {
            WallInfor wallInfor = new WallInfor();
            // step1: get the biggest length of outline
            var index = 0;
            var length = outLines[index].Length;
            int size = outLines.Count;
            for (int i = 1; i < size; i++)
            {
                if (DoubleExtensions.Larger(outLines[i].Length, length))
                {
                    index = i;
                    length = outLines[index].Length;
                }
            }

            var currentWallInfor = wallInfor;
            // step2: get the outline wall infor
            for (int i = 0; i < size; i++)
            {
                var line = outLines[(i + index) % size];
                // skip the line if it is in part exist wall infor
                if (!isPartOfWallinfor(line, wallInfor))
                {
                    List<LineSegment3d> innerlines = WallRecognizer.getWallline(line, allLines);
                    // set the wall infor
                    currentWallInfor.outline = line;
                    currentWallInfor.innerlines = innerlines;
                    currentWallInfor.next = new WallInfor();
                    currentWallInfor = currentWallInfor.next;
                }
            }

            return wallInfor;
        }

        public static bool isPartOfWallinfor(LineSegment3d line, WallInfor wallInfor)
        {
            WallInfor tmpWallInfor = wallInfor;
            while (tmpWallInfor != null)
            {
                List<Point3d> points = new List<Point3d>();
                if (tmpWallInfor.outline != null)
                {
                    if (!points.Contains(tmpWallInfor.outline.StartPoint))
                    {
                        points.Add(tmpWallInfor.outline.StartPoint);
                    }
                    if (!points.Contains(tmpWallInfor.outline.EndPoint))
                    {
                        points.Add(tmpWallInfor.outline.EndPoint);
                    }
                }

                if (tmpWallInfor.innerlines != null)
                {
                    foreach (LineSegment3d innerline in tmpWallInfor.innerlines)
                    {
                        if (!points.Contains(innerline.StartPoint))
                        {
                            points.Add(innerline.StartPoint);
                        }

                        if (!points.Contains(innerline.EndPoint))
                        {
                            points.Add(innerline.EndPoint);
                        }
                    }
                }

                if (points.Contains(line.StartPoint) && points.Contains(line.EndPoint))
                {
                    return true;
                }

                tmpWallInfor = tmpWallInfor.next;
            }

            return false;
        }

        public static void updateInnerLines(LineSegment3d line, double dist, List<LineSegment3d> innerlines)
        {
            bool haveLinesOverlapped = false;
            foreach (LineSegment3d innerLine in innerlines)
            {
                if (WallRecognizer.isSegmentsProjectionOverlapped(line, innerLine))
                {
                    haveLinesOverlapped = true;
                    break;
                }
            }

            if (haveLinesOverlapped)
            {
                // update the innerlines
                List<LineSegment3d> preparelines = new List<LineSegment3d>();
                foreach (LineSegment3d innerLine in innerlines)
                {
                    if (WallRecognizer.isSegmentsProjectionOverlapped(line, innerLine))
                    {
                        Line3d innerline3d = WallRecognizer.toLine3d(innerLine.StartPoint, innerLine.EndPoint);
                        double innerDist = innerline3d.GetDistanceTo(line.StartPoint);
                        if (DoubleExtensions.Larger(innerDist, dist))
                        {
                            if (!preparelines.Contains(line))
                            {
                                preparelines.Add(line);
                            }
                        }
                        else
                        {
                            if (!preparelines.Contains(innerLine))
                            {
                                preparelines.Add(innerLine);
                            }
                        }
                    }
                    else
                    {
                        if (!preparelines.Contains(line))
                        {
                            preparelines.Add(line);
                        }
                    }
                }

                // copy preparelines to innerlines
                innerlines.Clear();
                foreach (LineSegment3d innerLine in preparelines)
                {
                    innerlines.Add(innerLine);
                }
            }
            else
            {
                innerlines.Add(line);
            }
        }

        public static List<LineSegment3d> getWallline(LineSegment3d line, IEnumerable<LineSegment3d> entities)
        {
            double width = maxWallWidth + 1;

            // step 1: get the parallel lines in the loop: skip the colinear line
            var dir = line.Direction;
            var parallelLines = new List<LineSegment3d>();
            foreach (LineSegment3d tmpLine in entities)
            {
                if (line != tmpLine)
                {
                    if (dir.IsParallelTo(tmpLine.Direction) && 
                        (!tmpLine.IsOn(line.StartPoint) || tmpLine.IsOn(line.EndPoint))) // check if colinear line
                        parallelLines.Add(WallRecognizer.toLineSegment3d(tmpLine.StartPoint, tmpLine.EndPoint));
                }
            }

            List<LineSegment3d> innerlines = new List<LineSegment3d>();
            // step 2: get poper innerlines
            foreach (LineSegment3d line1 in parallelLines)
            {
                Line3d line3d = WallRecognizer.toLine3d(line1.StartPoint, line1.EndPoint);
                // skip the colinear line and the two lines should be projection overlapped
                if (!line3d.IsOn(line.StartPoint) && WallRecognizer.isSegmentsProjectionOverlapped(line1, line))
                {
                    double dist = line.GetDistanceTo(line1.StartPoint);
                    // pixel to millemeter
                    dist = (dist * MILLIMETER_TO_METER) / PIXEL_TO_M_FACTOR;
                    // the dist should > minWallWidth & < maxWallWidth
                    if (DoubleExtensions.Larger(dist, minWallWidth) && DoubleExtensions.Larger(maxWallWidth, dist))
                    {
                        //width = Math.Round(dist);

                        updateInnerLines(line1, dist, innerlines);
                    }
                }
            }

            return innerlines;
        }
    }
}
