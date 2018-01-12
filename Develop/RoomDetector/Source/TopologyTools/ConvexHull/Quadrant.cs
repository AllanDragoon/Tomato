using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TopologyTools.ConvexHull
{
	public abstract class Quadrant<T>
	{
		// ************************************************************************
		public PointInfo<T> FirstPoint;
        public PointInfo<T> LastPoint;
		public T RootPoint;

		public readonly List<PointInfo<T>> HullPoints = null;
        protected IEnumerable<IEnumerable<T>> _listOfListOfPoint;

	    protected Func<T, double> _xGet;
        protected Func<T, double> _yGet;

	    protected Func<double, double, T> _pointConstructor;
	    protected Func<T, T, bool> _pointEqual;

		// ************************************************************************
		// Very important the Quadrant should be always build in a way where dpiFirst has minus slope to center and dpiLast has maximum slope to center
        public Quadrant(IEnumerable<IEnumerable<T>> listOfListOfPoint, int initialResultGuessSize, 
            Func<T,double> xGet, Func<T, double> yGet, Func<double, double, T> pointConstructor, Func<T,T,bool> pointEqual)
		{
		    _xGet = xGet;
		    _yGet = yGet;
            _pointEqual = pointEqual;
		    _pointConstructor = pointConstructor;

			_listOfListOfPoint = listOfListOfPoint;
			HullPoints = new List<PointInfo<T>>(initialResultGuessSize);
		}

		// ************************************************************************
		/// <summary>
		/// Initialize every values needed to extract values that are parts of the convex hull.
		/// This is where the first pass of all values is done the get maximum in every directions (x and y).
		/// </summary>
		protected abstract void SetQuadrantLimits();

		// ************************************************************************
		public void Calc(bool isSkipSetQuadrantLimits = false)
		{
			if (! _listOfListOfPoint.Any() || ! _listOfListOfPoint.First().Any())
			{
				// There is no points at all. Hey don't try to crash me.
				return;
			}

			if (! isSkipSetQuadrantLimits)
			{
				SetQuadrantLimits();
			}

			// Begin : General Init
			HullPoints.Add(FirstPoint);
			if (FirstPoint.Equals(LastPoint)) 
			{
				return; // Case where for weird distribution lilke triangle or diagonal. This quadrant will have no point
			}
			HullPoints.Add(LastPoint);

			FirstPoint.SlopeToNext = Geometry.CalcSlope(FirstPoint.X, FirstPoint.Y, LastPoint.X, LastPoint.Y);
			LastPoint.SlopeToNext = GetLastPointSlopeToNext(); //  double.MaxValue;

			// Main Loop to extract ConvexHullPoints
			foreach (var enumOfPoints in _listOfListOfPoint)
			{
				foreach (T point in enumOfPoints)
				{
					if (!IsGoodQuadrantForPoint(point))
					{
						continue;
					}

					TryAdd(_xGet(point), _yGet(point));
				}
			}
		}

		// ************************************************************************
		protected abstract void TryAdd(double x, double y);

		// ************************************************************************
		protected abstract double GetLastPointSlopeToNext();

		// ************************************************************************
		protected abstract bool IsGoodQuadrantForPoint(T pt);

		// ************************************************************************
		/// <summary>
		/// 
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="indexLow">Prefiltered index - value should be between indexMin and indexMax</param>
		/// <param name="indexHi">Prefiltered index - value should be between indexMin and indexMax</param>
		/// <returns>True if can be rejected otherwise false</returns>
		//protected abstract bool IsPossibleToRejectPoint(double x, double y, out int indexLow, out int indexHi);

		// ************************************************************************
		public abstract bool IsValueCannotBeConvexValueToAnotherOne(double x, double y, double xRef, double yRef);

		// ************************************************************************

	}
}
