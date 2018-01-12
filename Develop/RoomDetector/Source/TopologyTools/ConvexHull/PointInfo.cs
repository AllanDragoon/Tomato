using System;

namespace TopologyTools.ConvexHull
{
    public class PointInfo<T>
    {
        private T _point;
        private Func<T, double> _xGet;
        private Func<T, double> _yGet;

        private Func<T, T, bool> _pointEqual; 

        private double _slopeToNext = double.NaN;
        public double SlopeToNext
        {
            get { return _slopeToNext; }
            set { _slopeToNext = value; }
        }

        public T Point
        {
            get { return _point; }
        }

        public double X
        {
            get { return _xGet(_point); }
        }

        public double Y
        {
            get { return _yGet(_point); }
        }

        public PointInfo(T point, Func<T, double> xGet, Func<T, double> yGet, Func<T, T, bool> pointEqual, double slopeToNext)
        {
            _point = point;
            _xGet = xGet;
            _yGet = yGet;
            _pointEqual = pointEqual;
            SlopeToNext = slopeToNext;
        }

        public PointInfo(T point, Func<T, double> xGet, Func<T, double> yGet, Func<T, T, bool> pointEqual)
            : this(point, xGet, yGet, pointEqual, double.NaN)
        {
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj is PointInfo<T>)
            {
                return _pointEqual(this.Point, ((PointInfo<T>)obj).Point);
            }

            if (obj is T)
            {
                return _pointEqual(this.Point, ((T)obj));
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _point.GetHashCode();
        }

        public override string ToString()
        {
            return _point.ToString();
        }
    }

}
