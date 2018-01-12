using System;

namespace TopologyTools.Utils
{
    internal static class DoubleExtensions
    {
        const double STolerance = 1e-06;

        /// <summary>
        /// Extension method to compare double values with a small tolerance to indicate "virtual equality"
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool EqualsWithTol(this double left, double right, double dTol = STolerance)
        {
            return Math.Abs(left - right) <= dTol;
        }

        public static bool EqualsWithTol(this double? left, double right, double dTol = STolerance)
        {
            var leftValue = double.NaN;
            if (left.HasValue)
                leftValue = left.Value;

            return Math.Abs(leftValue - right) <= dTol;
        }

        public static bool EqualsWithTol(this double? left, double? right, double dTol = STolerance)
        {
            var leftValue = double.NaN;
            if (left.HasValue)
                leftValue = left.Value;

            var rightValue = double.NaN;
            if (right.HasValue)
                rightValue = right.Value;

            return Math.Abs(leftValue - rightValue) <= dTol;
        }

        /// <summary>
        /// Extension method to compare decimal values with a small tolerance to indicate "virtual equality"
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="dTol"></param>
        /// <returns></returns>
        public static bool EqualsWithTol(this decimal left, decimal right, double dTol = STolerance)
        {
            return Math.Abs(left - right) <= (decimal)dTol;
        }

        /// <summary>
        /// Extension method to determine whether a double value is larger than another double.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool LargerWithTol(this double left, double right, double dTol = STolerance)
        {
            return (left > right) && !left.EqualsWithTol(right, dTol);
        }

        public static bool LargerOrEqualWithTol(this double left, double right, double dTol = STolerance)
        {
            return left.LargerWithTol(right, dTol) || left.EqualsWithTol(right, dTol);
        }

        /// <summary>
        /// Extension method to determine whether a double value is smaller than another double.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool SmallerWithTol(this double left, double right, double dTol = STolerance)
        {
            return (left < right) && !left.EqualsWithTol(right, dTol);
        }

        public static bool SmallerOrEqualWithTol(this double left, double right, double dTol = STolerance)
        {
            return left.SmallerWithTol(right, dTol) || left.EqualsWithTol(right, dTol);
        }

        public static double RadiansToDegrees(this double rads)
        {
            return rads * 57.295779513082323;
        }

        public static double DegreesToRadians(this double degrees)
        {
            return degrees * 0.017453292519943295;
        }
    }
}
