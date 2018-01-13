using System;

namespace LS.MapClean.Addin.Utils
{
    public static class DoubleExtensions
    {
        private static double _sTolerance = 1e-06;
        public static double STolerance
        {
            get { return _sTolerance; }
            set { _sTolerance = value; }
        }
        /// <summary>
        /// Extension method to compare double values with a small tolerance to indicate "virtual equality"
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool EqualsWithTolerance(this double left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return Math.Abs(left - right) <= dTol;
        }

        public static bool EqualsWithTolerance(this double? left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;

            var leftValue = double.NaN;
            if (left.HasValue)
                leftValue = left.Value;

            return Math.Abs(leftValue - right) <= dTol;
        }

        public static bool EqualsWithTolerance(this double? left, double? right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
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
        public static bool EqualsWithTolerance(this decimal left, decimal right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return Math.Abs(left - right) <= (decimal)dTol;
        }

        /// <summary>
        /// Extension method to determine whether a double value is larger than another double.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool Larger(this double left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return (left > right) && !left.EqualsWithTolerance(right, dTol);
        }

        public static bool LargerOrEqual(this double left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return left.Larger(right, dTol) || left.EqualsWithTolerance(right, dTol);
        }

        /// <summary>
        /// Extension method to determine whether a double value is smaller than another double.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool Smaller(this double left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return (left < right) && !left.EqualsWithTolerance(right, dTol);
        }

        public static bool SmallerOrEqual(this double left, double right, double? dTolerance = null)
        {
            double dTol = dTolerance != null ? dTolerance.Value : STolerance;
            return left.Smaller(right, dTol) || left.EqualsWithTolerance(right, dTol);
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

    public static class NumberUtils
    {
        public static bool NumberToBool(decimal? number)
        {
            return number != null && number.Value != 0;
        }

        public static bool NumberToBool(int? number)
        {
            return number != null && number.Value != 0;
        }

        public static int BoolToNumber(bool b)
        {
            return b ? 1 : 0;
        }
    }
}
