using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DbxUtils.Utils
{
    // http://through-the-interface.typepad.com/through_the_interface/2008/02/robotic-hatchin.html
    // Check if a point is in a curve
    public class PointInCurve
    {
        enum IncidenceType
        {
            ToLeft = 0,
            ToRight = 1,
            ToFront = 2,
            Unknown
        };

        static IncidenceType CurveIncidence(Curve cur, double param, Vector3d dir, Vector3d normal)
        {
            Vector3d deriv1 = cur.GetFirstDerivative(param);
            if (deriv1.IsParallelTo(dir)) 
            {
                // Need second degree analysis
                Vector3d deriv2 = cur.GetSecondDerivative(param);
                if (deriv2.IsZeroLength() || deriv2.IsParallelTo(dir))
                    return IncidenceType.ToFront;
                    
                if (deriv2.CrossProduct(dir).DotProduct(normal) < 0)
                    return IncidenceType.ToRight;
                return IncidenceType.ToLeft;
            }


            if (deriv1.CrossProduct(dir).DotProduct(normal) < 0)
                return IncidenceType.ToLeft;

            return IncidenceType.ToRight;
        }

        static public bool IsInsideCurve(Curve cur, Point3d testPt)
        {
            if (!cur.Closed)
                // Cannot be inside
                return false;

            var poly2d = cur as Polyline2d;
            if (poly2d != null && poly2d.PolyType != Poly2dType.SimplePoly)
                // Not supported
                return false;
            var ptOnCurve = cur.GetClosestPointTo(testPt, false);
            if (Tolerance.Equals(testPt, ptOnCurve))
                return true;

            // Check it's planar
            var plane = cur.GetPlane();
            if (!cur.IsPlanar)
                return false;

            // Make the test ray from the plane
            var normal = plane.Normal;
            var testVector = normal.GetPerpendicularVector();

            var ray = new Ray {BasePoint = testPt, UnitDir = testVector};
            var intersectionPoints = new Point3dCollection();

            // Fire the ray at the curve
            cur.IntersectWith(ray, Intersect.OnBothOperands, intersectionPoints, 0, 0);
            ray.Dispose();

            int numberOfInters = intersectionPoints.Count;
            if (numberOfInters == 0)
                // Must be outside
                return false;

            int nGlancingHits = 0;
            const double epsilon = 2e-6; // (trust me on this)
            for (int i = 0; i < numberOfInters; i++)
            {
                // Get the first point, and get its parameter
                Point3d hitPt = intersectionPoints[i];
                double hitParam = cur.GetParameterAtPoint(hitPt);

                double inParam = hitParam - epsilon;
                double outParam = hitParam + epsilon;
                IncidenceType inIncidence = CurveIncidence(cur, inParam, testVector, normal);
                IncidenceType outIncidence = CurveIncidence(cur, outParam, testVector, normal);

                if ((inIncidence == IncidenceType.ToRight && outIncidence == IncidenceType.ToLeft) ||
                    (inIncidence == IncidenceType.ToLeft &&
                    outIncidence == IncidenceType.ToRight))
                    nGlancingHits++;
            }

            return ((numberOfInters + nGlancingHits) % 2 == 1);
        }
    }
}
