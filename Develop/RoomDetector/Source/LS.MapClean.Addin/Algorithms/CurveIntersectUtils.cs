using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    class CurveIntersectUtils
    {
        /// <summary>
        /// Calculate intersection point of two line.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static IntersectionInfo InsersectLines(Line source, Line target)
        {
            var points = new Point3dCollection();
            source.IntersectWith(target, Intersect.ExtendBoth, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count <= 0)
                return null;

            // NOTE: Use Line's GetParameterAtPoint will throw exception if the intersect point is 
            // on the line's extension, but LineSegment3d.GetParameterOf is available, so I convert
            // the Line to LineSegment3d here.
            var intersectPoint = points[0];
            var sourceLineSegment = new LineSegment3d(source.StartPoint, source.EndPoint);
            var targetLineSegment = new LineSegment3d(target.StartPoint, target.EndPoint);

            var sourceParam = sourceLineSegment.GetParameterOf(intersectPoint);
            var sourceExtendType = ParamToExtendTypeForLine(sourceParam);

            var targetParam = targetLineSegment.GetParameterOf(intersectPoint);
            var targetExtendType = ParamToExtendTypeForLine(targetParam);

            var result = new IntersectionInfo(sourceExtendType, targetExtendType, intersectPoint);
            return result;
        }

        public static IEnumerable<IntersectionInfo> IntersectLineArc(Line line, Arc arc, ExtendType? coerceArcExtendType)
        {
            var points = new Point3dCollection();
            line.IntersectWith(arc, Intersect.ExtendBoth, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count <= 0)
                return new IntersectionInfo[0];

            // NOTE: Use Line's GetParameterAtPoint will throw exception if the intersect point is 
            // on the line's extension, but LineSegment3d.GetParameterOf is available, so I convert
            // the Line to LineSegment3d here.
            var lineSegment = new LineSegment3d(line.StartPoint, line.EndPoint);
            var circularArc = new CircularArc3d(arc.Center, arc.Normal, arc.Normal.GetPerpendicularVector(), arc.Radius,
                arc.StartAngle, arc.EndAngle);

            var result = new List<IntersectionInfo>();
            foreach (Point3d point in points)
            {
                var lineParam = lineSegment.GetParameterOf(point);
                var lineExtendType = ParamToExtendTypeForLine(lineParam);

                var arcParam = circularArc.GetParameterOf(point);
                var arcExtendType = ParamToExtendTypeForArc(circularArc, arcParam, coerceArcExtendType);
                result.Add(new IntersectionInfo(lineExtendType, arcExtendType, point));
            }

            return result;
        }

        public static IEnumerable<IntersectionInfo> IntersectArcs(Arc source, ExtendType? coerceSourceExtendType, Arc target, ExtendType? coerceTargetExtendType)
        {
            var points = new Point3dCollection();
            source.IntersectWith(target, Intersect.ExtendBoth, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count <= 0)
                return new IntersectionInfo[0];

            // NOTE: Use Arc's GetParameterAtPoint will throw exception if the intersect point is 
            // on the Arc's extension, but CircularArc3d.GetParameterOf is available, so I convert
            // the Arc to CircularArc3d here. 
            var sourceArc = new CircularArc3d(source.Center, source.Normal, source.Normal.GetPerpendicularVector(),
                source.Radius, source.StartAngle, source.EndAngle);
            var targetArc = new CircularArc3d(target.Center, target.Normal, target.Normal.GetPerpendicularVector(),
                target.Radius, target.StartAngle, target.EndAngle);

            var result = new List<IntersectionInfo>();
            foreach (Point3d point in points)
            {
                var sourceParam = sourceArc.GetParameterOf(point);
                var sourceExtendType = ParamToExtendTypeForArc(sourceArc, sourceParam, coerceSourceExtendType);
                var targetParam = targetArc.GetParameterOf(point);
                var targetExtendType = ParamToExtendTypeForArc(targetArc, targetParam, coerceTargetExtendType);
            }
            return result;
        }

        /// <summary>
        /// Paramameter on line segment to extend type.
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static ExtendType ParamToExtendTypeForLine(double param)
        {
            var result = ExtendType.None;
            if (param.Larger(1.0))
                result = ExtendType.ExtendEnd;
            else if (param.Smaller(0.0))
                result = ExtendType.ExtendStart;
            return result;
        }

        public static ExtendType ParamToExtendTypeForArc(CircularArc3d arc, double param, ExtendType? coerceExtendType)
        {
            var startParam = arc.GetParameterOf(arc.StartPoint);
            var endParam = arc.GetParameterOf(arc.EndPoint);

            var result = ExtendType.None;
            // If param is in the middle of startParam and endParam
            var val = (param - startParam)*(param - endParam);
            if (val.SmallerOrEqual(0.0))
            {
                result = ExtendType.None;
            }
            else
            {
                // If coerce extend type is not null.
                if (coerceExtendType != null)
                {
                    result = coerceExtendType.Value;
                }
                else
                {
                    // Determine which distance is shorter.
                    if (Math.Abs(param - startParam) >= Math.Abs(param - endParam))
                        result = ExtendType.ExtendEnd;
                    else
                        result = ExtendType.ExtendStart;
                }
            }
            return result;
        }
    }
}
