using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;

namespace TopologyTools.PolylineSimplify
{
    /// <summary>
    /// http://www.namekdev.net/2014/06/iterative-version-of-ramer-douglas-peucker-line-simplification-algorithm/
    /// https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm
    /// The Ramer–Douglas–Peucker algorithm (RDP) is an algorithm for reducing the number of points in a curve
    /// that is approximated by a series of points.
    /// </summary>
    public class RamerDouglasPeucker
    {
        public static List<Point3d> DouglasPeuckerRecursive(List<Point3d> points, int startIndex, int lastIndex, double epsilon)
        {
            var dmax = 0.0;
            int index = startIndex;

            for (int i = index + 1; i < lastIndex; ++i)
            {
                var d = PointLineDistance(points[i], points[startIndex], points[lastIndex]);
                if (d > dmax)
                {
                    index = i;
                    dmax = d;
                }
            }

            if (dmax > epsilon)
            {
                var res1 = DouglasPeuckerRecursive(points, startIndex, index, epsilon);
                var res2 = DouglasPeuckerRecursive(points, index, lastIndex, epsilon);

                var finalRes = new List<Point3d>();
                for (int i = 0; i < res1.Count - 1; ++i)
                {
                    finalRes.Add(res1[i]);
                }

                for (int i = 0; i < res2.Count; ++i)
                {
                    finalRes.Add(res2[i]);
                }

                return finalRes;
            }
            else
            {
                return new List<Point3d>(new Point3d[] { points[startIndex], points[lastIndex] });
            }
        }

        private static double PointLineDistance(Point3d point, Point3d start, Point3d end)
        {
            if (start == end)
            {
                return point.DistanceTo(start);
            }

            var n = Math.Abs((end.X - start.X) * (start.Y - point.Y) - (start.X - point.X) * (end.Y - start.Y));
            var d = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));

            return n / d;
        }

        /// <summary>
        /// Ramer-Douglas-Peucker algorithm which reduces a series of points
        /// to a simplified version that loses detail,
        /// but maintains the general shape of the series.
        /// </summary>
        private static BitArray DouglasPeucker(List<Point3d> points, int startIndex, int lastIndex, double epsilon)
        {
            var stk = new Stack<KeyValuePair<int, int>>();
            stk.Push(new KeyValuePair<int, int>(startIndex, lastIndex));

            int globalStartIndex = startIndex;
            var bitArray = new BitArray(lastIndex - startIndex + 1, true);

            while (stk.Count > 0)
            {
                startIndex = stk.Peek().Key;
                lastIndex = stk.Peek().Value;
                stk.Pop();

                var dmax = 0.0;
                int index = startIndex;

                for (int i = index + 1; i < lastIndex; ++i)
                {
                    if (bitArray[i - globalStartIndex])
                    {
                        var d = PointLineDistance(points[i], points[startIndex], points[lastIndex]);

                        if (d > dmax)
                        {
                            index = i;
                            dmax = d;
                        }
                    }
                }

                if (dmax > epsilon)
                {
                    stk.Push(new KeyValuePair<int, int>(startIndex, index));
                    stk.Push(new KeyValuePair<int, int>(index, lastIndex));
                }
                else
                {
                    for (int i = startIndex + 1; i < lastIndex; ++i)
                    {
                        bitArray[i - globalStartIndex] = false;
                    }
                }
            }

            return bitArray;
        }

        public static List<Point3d> DouglasPeucker(List<Point3d> points, double epsilon)
        {
            var bitArray = DouglasPeucker(points, 0, points.Count - 1, epsilon);
            var resList = new List<Point3d>();

            for (int i = 0, n = points.Count; i < n; ++i)
            {
                if (bitArray[i])
                {
                    resList.Add(points[i]);
                }
            }
            return resList;
        }

        public static List<Point3d> DouglasPeuckerForLoop(List<Point3d> points, double epsilon)
        {
            // DouglasPeucker主要针对的是非闭合线，因为首末两点永远是保留下来的。
            // 但是如果是闭合线的话，首末两点过就有可能没必要保留下来。
            var result = DouglasPeucker(points, epsilon);
            // 再处理一下首末两点
            if (result.Count <= 3)
                return result;
            // 将第一个点移到最后
            var first = result[0];
            result.RemoveAt(0);
            result.Add(first);
            // 将第二个点也移到最后
            var second = result[0];
            result.RemoveAt(0);
            result.Add(second);

            // 再运行一遍DouglasPeucker
            result = DouglasPeucker(result, epsilon);

            // 保证顶点的顺序
            var lastIndex = result.LastIndexOf(second);
            if (lastIndex != -1)
            {
                result.RemoveAt(lastIndex);
                result.Insert(0, second);
            }

            lastIndex = result.LastIndexOf(first);
            if (lastIndex != -1)
            {
                result.RemoveAt(lastIndex);
                result.Insert(0, first);
            }
            return result;
        }
    }
}
