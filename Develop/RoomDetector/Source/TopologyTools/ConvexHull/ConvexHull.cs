using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TopologyTools.Utils;

namespace TopologyTools.ConvexHull
{
	// ******************************************************************
	public class ConvexHull<T>
	{
		// Quadrant: Q2 | Q1
		//	         -------
		//           Q3 | Q4

		private Quadrant<T> _q1;
        private Quadrant<T> _q2;
        private Quadrant<T> _q3;
        private Quadrant<T> _q4;

		private IEnumerable<IEnumerable<T>> _listOfListOfPoint;
		private bool _shouldCloseTheGraph;

	    private Func<T, double> _xGet;
	    private Func<T, double> _yGet;
	    private Func<double, double, T> _pointConstructor;
	    private Func<T, T, bool> _pointEqual; 

		// ******************************************************************
        public ConvexHull(IEnumerable<IEnumerable<T>> listOfListOfPoint,
            Func<T, double> xGet, Func<T, double> yGet, Func<double, double, T> pointConstructor, Func<T, T, bool> pointEqual,
            bool shouldCloseTheGraph = true, int initialResultGuessSize = 0)
        {
            _xGet = xGet;
            _yGet = yGet;
            _pointConstructor = pointConstructor;
            _pointEqual = pointEqual;
            Init(listOfListOfPoint, shouldCloseTheGraph, initialResultGuessSize);
        }

        // ******************************************************************
        public ConvexHull(IEnumerable<T> listOfPoint,
            Func<T, double> xGet, Func<T, double> yGet, Func<double, double, T> pointConstructor, Func<T, T, bool> pointEqual,
            bool shouldCloseTheGraph = true, int initialResultGuessSize = 0)
        {
            _xGet = xGet;
            _yGet = yGet;
            _pointConstructor = pointConstructor;
            _pointEqual = pointEqual;

            IEnumerable<T>[] listOfListOfPoint = new IEnumerable<T>[1];
            listOfListOfPoint[0] = listOfPoint;

            Init(listOfListOfPoint, shouldCloseTheGraph, initialResultGuessSize);
        }

		// ******************************************************************
        private void Init(IEnumerable<IEnumerable<T>> listOfListOfPoint, bool shouldCloseTheGraph, int initialResultGuessSize)
		{
			_listOfListOfPoint = listOfListOfPoint;
			_shouldCloseTheGraph = shouldCloseTheGraph;

			if (initialResultGuessSize <= 0 && !IsZeroData())
			{
				int totalPointCount = 0;
				foreach (var enumOfPoint in listOfListOfPoint)
				{
					totalPointCount += enumOfPoint.Count();
				}
				initialResultGuessSize = Math.Min((int)Math.Sqrt(totalPointCount), 100);
			}

			_q1 = new QuadrantSpecific1<T>(listOfListOfPoint, initialResultGuessSize, _xGet, _yGet, _pointConstructor, _pointEqual);
            _q2 = new QuadrantSpecific2<T>(listOfListOfPoint, initialResultGuessSize, _xGet, _yGet, _pointConstructor, _pointEqual);
            _q3 = new QuadrantSpecific3<T>(listOfListOfPoint, initialResultGuessSize, _xGet, _yGet, _pointConstructor, _pointEqual);
            _q4 = new QuadrantSpecific4<T>(listOfListOfPoint, initialResultGuessSize, _xGet, _yGet, _pointConstructor, _pointEqual);
		}

		// ******************************************************************
		private int _count = -1;
		public int Count
		{
			get
			{
				if (_count == -1)
				{
					foreach (IEnumerable<T> listOfPoint in this._listOfListOfPoint)
					{
						_count += listOfPoint.Count();
					}
				}

				return _count;
			}
		}

		// ******************************************************************
		/// <summary>
		/// 
		/// </summary>
		/// <param name="threadUsage">Using ConvexHullThreadUsage.All will only use all thread for the first pass (se quadrant limits) then use only 4 threads for pass 2 (which is the actual limit).</param>
		public void CalcConvexHull(ConvexHullThreadUsage threadUsage = ConvexHullThreadUsage.OneOrFour)
		{
			if (IsZeroData())
			{
				return;
			}

			if (threadUsage == ConvexHullThreadUsage.AutoSelect || threadUsage == ConvexHullThreadUsage.OneOrFour)
			{
				if (Environment.ProcessorCount == 1)
				{
					threadUsage = ConvexHullThreadUsage.OnlyOne;
				}
				// It's around 10 000 000 points on a 12 cores that some advantages really start to appear
				else if (threadUsage == ConvexHullThreadUsage.OneOrFour || Environment.ProcessorCount <= 4 || this.Count < 10000000)
				{
					threadUsage = ConvexHullThreadUsage.FixedFour;
				}
				else
				{
					threadUsage = ConvexHullThreadUsage.All;
				}
			}

			// There is no need to start more than 1 thread. It will not be usefull on a single core machine.
			if (threadUsage == ConvexHullThreadUsage.OnlyOne)
			{
				_q1.Calc();
				_q2.Calc();
				_q3.Calc();
				_q4.Calc();
			}
			else
			{
				bool isSkipSetQuadrantLimit = false;
				if (threadUsage == ConvexHullThreadUsage.All)
				{
					isSkipSetQuadrantLimit = true;

					SetQuadrantLimitsUsingAllThreads();
				}

				Quadrant<T>[] quadrants = new Quadrant<T>[4];
				quadrants[0] = _q1;
				quadrants[1] = _q2;
				quadrants[2] = _q3;
				quadrants[3] = _q4;

				Task[] tasks = new Task[4];
				for (int n = 0; n < tasks.Length; n++)
				{
					int nLocal = n; // Prevent Lambda internal closure error.
					tasks[n] = Task.Factory.StartNew(() => quadrants[nLocal].Calc(isSkipSetQuadrantLimit));
				}
				Task.WaitAll(tasks);
			}
		}

		private Limit<T> _limit = null;
		// ******************************************************************
		// For usage of Parallel func, I highly suggest: Stephen Toub: Patterns of parallel programming ==> Just Awsome !!!
		// But its only my own fault if I'm not using it at its full potential...
		private void SetQuadrantLimitsUsingAllThreads()
		{
			T pt = this._listOfListOfPoint.First().First();
			_limit = new Limit<T>(pt);

			int coreCount = Environment.ProcessorCount;

			Task[] tasks = new Task[coreCount];
			for (int n = 0; n < tasks.Length; n++)
			{
				int nLocal = n; // Prevent Lambda internal closure error.
				tasks[n] = Task.Factory.StartNew(() =>
				{
					Limit<T> limit = _limit.Copy();
					FindLimits(_listOfListOfPoint, nLocal, coreCount, limit);
					AggregateLimits(limit);
				});
			}
			Task.WaitAll(tasks);

            _q1.FirstPoint = new PointInfo<T>(_limit.Q1Right, _xGet, _yGet, _pointEqual);
            _q1.LastPoint = new PointInfo<T>(_limit.Q1Top, _xGet, _yGet, _pointEqual);
            _q2.FirstPoint = new PointInfo<T>(_limit.Q2Top, _xGet, _yGet, _pointEqual);
            _q2.LastPoint = new PointInfo<T>(_limit.Q2Left, _xGet, _yGet, _pointEqual);
            _q3.FirstPoint = new PointInfo<T>(_limit.Q3Left, _xGet, _yGet, _pointEqual);
            _q3.LastPoint = new PointInfo<T>(_limit.Q3Bottom, _xGet, _yGet, _pointEqual);
            _q4.FirstPoint = new PointInfo<T>(_limit.Q4Bottom, _xGet, _yGet, _pointEqual);
            _q4.LastPoint = new PointInfo<T>(_limit.Q4Right, _xGet, _yGet, _pointEqual);

			_q1.RootPoint = _pointConstructor(_q1.LastPoint.X, _q1.FirstPoint.Y);
            _q2.RootPoint = _pointConstructor(_q2.FirstPoint.X, _q2.LastPoint.Y);
            _q3.RootPoint = _pointConstructor(_q3.LastPoint.X, _q3.FirstPoint.Y);
            _q4.RootPoint = _pointConstructor(_q4.FirstPoint.X, _q4.LastPoint.Y);
		}

		// ******************************************************************
        private Limit<T> FindLimits(IEnumerable<IEnumerable<T>> listOfListOfPoint, int start, int offset, Limit<T> limit)
		{
			foreach (var listOfPoint in listOfListOfPoint)
			{
			    var count = listOfPoint.Count();
                for (int index = start; index < count; index += offset)
				{
					T pt = listOfPoint.ElementAt(index);

					double x = _xGet(pt);
					double y = _yGet(pt);

					// Top
                    if (y.LargerOrEqualWithTol(_yGet(limit.Q2Top)))
					{
						if (y.EqualsWithTol(_yGet(limit.Q2Top))) // Special
						{
							if (y .EqualsWithTol( _yGet(limit.Q1Top)))
							{
								if (x .SmallerWithTol( _xGet(limit.Q2Top)))
								{
									limit.Q2Top = _pointConstructor(x, _yGet(limit.Q2Top));
								}
								else if (x .LargerWithTol(_xGet(limit.Q1Top)))
								{
									limit.Q1Top = _pointConstructor(x, _yGet(limit.Q1Top));
								}
							}
							else
							{
								if (x .SmallerWithTol( _xGet(limit.Q2Top)))
								{
                                    limit.Q1Top = _pointConstructor(_xGet(limit.Q2Top), _yGet(limit.Q2Top));
                                    limit.Q2Top = _pointConstructor(x, _yGet(limit.Q2Top));
								}
								else if (x.LargerWithTol(_xGet(limit.Q1Top)))
								{
								    limit.Q1Top = _pointConstructor(x, y);
								}
							}
						}
						else
						{
						    limit.Q2Top = _pointConstructor(x, y);
						}
					}

					// Bottom
					if (y .SmallerOrEqualWithTol( _yGet(limit.Q3Bottom)))
					{
						if (y.EqualsWithTol(_yGet(limit.Q3Bottom))) // Special
						{
                            if (y.EqualsWithTol(_yGet(limit.Q4Bottom)))
							{
								if (x .SmallerWithTol( _xGet(limit.Q3Bottom)))
								{
                                    limit.Q3Bottom = _pointConstructor(x, _yGet(limit.Q3Bottom));
								}
								else if (x .LargerWithTol( _xGet(limit.Q4Bottom)))
								{
								    limit.Q4Bottom = _pointConstructor(x, _yGet(limit.Q4Bottom));
								}
							}
							else
							{
								if (x .SmallerWithTol( _xGet(limit.Q3Bottom)))
								{
                                    limit.Q4Bottom = _pointConstructor(_xGet(limit.Q3Bottom), _yGet(limit.Q3Bottom));
                                    limit.Q3Bottom = _pointConstructor(x, _yGet(limit.Q3Bottom));
								}
								else if (x .LargerWithTol( _xGet(limit.Q3Bottom)))
								{
								    limit.Q4Bottom = _pointConstructor(x, y);
								}
							}
						}
						else
						{
						    limit.Q3Bottom = _pointConstructor(x, y);
						}
					}

					// Right
                    if (x.LargerOrEqualWithTol(_xGet(limit.Q4Right)))
					{
						if (x.EqualsWithTol(_xGet(limit.Q4Right))) // Special
						{
							if (x.EqualsWithTol(_xGet(limit.Q1Right)))
							{
								if (y .SmallerWithTol( _yGet(limit.Q4Right)))
								{
                                    limit.Q4Right = _pointConstructor(_xGet(limit.Q4Right), y);
								}
								else if (y .LargerWithTol(_yGet(limit.Q1Right)))
								{
								    limit.Q1Right = _pointConstructor(_xGet(limit.Q1Right), y);
								}
							}
							else
							{
								if (y .SmallerWithTol( _yGet(limit.Q4Right)))
								{
                                    limit.Q1Right = _pointConstructor(_xGet(limit.Q4Right), _yGet(limit.Q4Right));
									limit.Q4Right = _pointConstructor(_xGet(limit.Q4Right), y);
								}
								else if (y .LargerWithTol( _yGet(limit.Q1Right)))
								{
								    limit.Q1Right = _pointConstructor(x, y);
								}
							}
						}
						else
						{
						    limit.Q4Right = _pointConstructor(x, y);
						}
					}

					// Left
					if (x.SmallerOrEqualWithTol(_xGet(limit.Q3Left)))
					{
						if (x.EqualsWithTol(_xGet(limit.Q3Left))) // Special
						{
							if (x.EqualsWithTol(_xGet(limit.Q2Left)))
							{
								if (y .SmallerWithTol( _yGet(limit.Q3Left)))
								{
								    limit.Q3Left = _pointConstructor(_xGet(limit.Q3Left), y);
								}
								else if (y .LargerWithTol(_yGet(limit.Q2Left)))
								{
                                    limit.Q2Left = _pointConstructor(_xGet(limit.Q2Left), y);
								}
							}
							else
							{
								if (y .SmallerWithTol( _yGet(limit.Q3Left)))
								{
								    limit.Q2Left = _pointConstructor(_xGet(limit.Q3Left), _yGet(limit.Q3Left));

								    limit.Q3Left = _pointConstructor(_xGet(limit.Q3Left), y);
								}
								else if (y .LargerWithTol(_yGet(limit.Q2Left)))
								{
								    limit.Q2Left = _pointConstructor(x, y);
								}
							}
						}
						else
						{
						    limit.Q3Left = _pointConstructor(x, y);
						}
					}

					if (!_xGet(limit.Q2Left).EqualsWithTol(_xGet(limit.Q3Left)))
					{
                        limit.Q2Left = _pointConstructor(_xGet(limit.Q3Left), _yGet(limit.Q3Left));
					}

					if (!_xGet(limit.Q1Right).EqualsWithTol(_xGet(limit.Q4Right)))
					{
                        limit.Q1Right = _pointConstructor(_xGet(limit.Q4Right), _yGet(limit.Q4Right));
					}

                    if (!_yGet(limit.Q1Top).EqualsWithTol(_yGet(limit.Q2Top)))
					{
                        limit.Q1Top = _pointConstructor(_xGet(limit.Q2Top), _yGet(limit.Q2Top));
					}

					if (!_yGet(limit.Q4Bottom).EqualsWithTol(_yGet(limit.Q3Bottom)))
					{
                        limit.Q4Bottom = _pointConstructor(_xGet(limit.Q3Bottom), _yGet(limit.Q3Bottom));
					}

				}
			}

			return limit;
		}

		// ******************************************************************
		private Limit<T> FindLimits(T pt, ParallelLoopState state, Limit<T> limit)
		{
			double x = _xGet(pt);
			double y = _yGet(pt);

			// Top
            if (y.LargerOrEqualWithTol(_yGet(limit.Q2Top)))
			{
                if (y.EqualsWithTol(_yGet(limit.Q2Top))) // Special
				{
                    if (y.EqualsWithTol(_yGet(limit.Q1Top)))
					{
						if (x .SmallerWithTol( _xGet(limit.Q2Top)))
						{
                            limit.Q2Top = _pointConstructor(x, _yGet(limit.Q2Top));
						}
						else if (x .LargerWithTol( _xGet(limit.Q1Top)))
						{
                            limit.Q1Top = _pointConstructor(x, _yGet(limit.Q1Top));
						}
					}
					else
					{
						if (x .SmallerWithTol( _xGet(limit.Q2Top)))
						{
                            limit.Q1Top = _pointConstructor(_xGet(limit.Q2Top), _yGet(limit.Q2Top));

                            limit.Q2Top = _pointConstructor(x, _yGet(limit.Q2Top));
						}
						else if (x .LargerWithTol( _xGet(limit.Q1Top)))
						{
						    limit.Q1Top = _pointConstructor(x, y);
						}
					}
				}
				else
                {
                    limit.Q2Top = _pointConstructor(x, y);
				}
			}

			// Bottom
			if (y.SmallerOrEqualWithTol(_yGet(limit.Q3Bottom)))
			{
				if (y.EqualsWithTol(_yGet(limit.Q3Bottom))) // Special
				{
					if (y.EqualsWithTol(_yGet(limit.Q4Bottom)))
					{
						if (x .SmallerWithTol(_xGet(limit.Q3Bottom)))
						{
						    limit.Q3Bottom = _pointConstructor(x, _yGet(limit.Q3Bottom));
						}
						else if (x .LargerWithTol( _xGet(limit.Q4Bottom)))
						{
                            limit.Q4Bottom = _pointConstructor(x, _yGet(limit.Q4Bottom));
						}
					}
					else
					{
						if (x .SmallerWithTol( _xGet(limit.Q3Bottom)))
						{
                            limit.Q4Bottom = _pointConstructor(_xGet(limit.Q3Bottom), _yGet(limit.Q3Bottom));

                            limit.Q3Bottom = _pointConstructor(x, _yGet(limit.Q3Bottom));
						}
						else if (x .LargerWithTol( _xGet(limit.Q3Bottom)))
						{
						    limit.Q4Bottom = _pointConstructor(x, y);
						}
					}
				}
				else
				{
				    limit.Q3Bottom = _pointConstructor(x, y);
				}
			}

			// Right
			if (x.LargerOrEqualWithTol(_xGet(limit.Q4Right)))
			{
                if (x.EqualsWithTol( _xGet(limit.Q4Right))) // Special
				{
					if (x.EqualsWithTol( _xGet(limit.Q1Right)))
					{
						if (y .SmallerWithTol( _yGet(limit.Q4Right)))
						{
                            limit.Q4Right = _pointConstructor(_xGet(limit.Q4Right), y);
						}
						else if (y .LargerWithTol( _yGet(limit.Q1Right)))
						{
						    limit.Q1Right = _pointConstructor(_xGet(limit.Q1Right), y);
						}
					}
					else
					{
						if (y .SmallerWithTol( _yGet(limit.Q4Right)))
						{
                            limit.Q1Right = _pointConstructor(_xGet(limit.Q4Right), _yGet(limit.Q4Right));
						    limit.Q4Right = _pointConstructor(_xGet(limit.Q4Right), y);
						}
						else if (y .LargerWithTol(_yGet(limit.Q1Right)))
						{
						    limit.Q1Right = _pointConstructor(x, y);
						}
					}
				}
				else
                {
                    limit.Q4Right = _pointConstructor(x, y);
				}
			}

			// Left
            if (x.SmallerOrEqualWithTol(_xGet(limit.Q3Left)))
			{
				if (x.EqualsWithTol(_xGet(limit.Q3Left))) // Special
				{
					if (x.EqualsWithTol( _xGet(limit.Q2Left)))
					{
						if (y .SmallerWithTol( _yGet(limit.Q3Left)))
						{
						    limit.Q3Left = _pointConstructor(_xGet(limit.Q3Left), y);
						}
						else if (y .LargerWithTol( _yGet(limit.Q2Left)))
						{
                            limit.Q2Left = _pointConstructor(_xGet(limit.Q2Left), y);
						}
					}
					else
					{
						if (y .SmallerWithTol( _yGet(limit.Q3Left)))
						{
                            limit.Q2Left = _pointConstructor(_xGet(limit.Q3Left), _yGet(limit.Q3Left));
                            limit.Q3Left = _pointConstructor(_xGet(limit.Q3Left), y);
						}
						else if (y .LargerWithTol( _yGet(limit.Q2Left)))
						{
						    limit.Q2Left = _pointConstructor(x, y);
						}
					}
				}
				else
				{
				    limit.Q3Left = _pointConstructor(x, y);
				}
			}

			if (!_xGet(limit.Q2Left).EqualsWithTol(_xGet(limit.Q3Left)))
			{
                limit.Q2Left = _pointConstructor(_xGet(limit.Q3Left), _yGet(limit.Q3Left));
			}

			if (!_xGet(limit.Q1Right).EqualsWithTol( _xGet(limit.Q4Right)))
			{
                limit.Q1Right = _pointConstructor(_xGet(limit.Q4Right), _yGet(limit.Q4Right));
			}

            if (!_yGet(limit.Q1Top).EqualsWithTol(_yGet(limit.Q2Top))) 
			{
                limit.Q1Top = _pointConstructor(_xGet(limit.Q2Top), _yGet(limit.Q2Top));
			}

			if (!_yGet(limit.Q4Bottom).EqualsWithTol(_yGet(limit.Q3Bottom)))
			{
                limit.Q4Bottom = _pointConstructor(_xGet(limit.Q3Bottom), _yGet(limit.Q3Bottom));
			}

			return limit;
		}

		// ******************************************************************
		private object _findLimitFinalLock = new object();

		private void AggregateLimits(Limit<T> limit)
		{
			lock (_findLimitFinalLock)
			{
                if (_xGet(limit.Q1Right).LargerOrEqualWithTol(_xGet(_limit.Q1Right)))
				{
					if (_xGet(limit.Q1Right).EqualsWithTol( _xGet(_limit.Q1Right)))
					{
						if (_yGet(limit.Q1Right).LargerWithTol(_yGet(_limit.Q1Right)))
						{
							_limit.Q1Right = limit.Q1Right;
						}
					}
					else
					{
						_limit.Q1Right = limit.Q1Right;
					}
				}

				if (_xGet(limit.Q4Right) .LargerWithTol( _xGet(_limit.Q4Right)))
				{
					if (_xGet(limit.Q4Right) .EqualsWithTol( _xGet(_limit.Q4Right)))
					{
						if (_yGet(limit.Q4Right) .SmallerWithTol( _yGet(_limit.Q4Right)))
						{
							_limit.Q4Right = limit.Q4Right;
						}
					}
					else
					{
						_limit.Q4Right = limit.Q4Right;
					}
				}

				if (_xGet(limit.Q2Left).SmallerWithTol(_xGet(_limit.Q2Left)))
				{
					if (_xGet(limit.Q2Left) .EqualsWithTol( _xGet(_limit.Q2Left)))
					{
						if (_yGet(limit.Q2Left) .LargerWithTol( _yGet(_limit.Q2Left)))
						{
							_limit.Q2Left = limit.Q2Left;
						}
					}
					else
					{
						_limit.Q2Left = limit.Q2Left;
					}
				}

				if (_xGet(limit.Q3Left) .SmallerWithTol( _xGet(_limit.Q3Left)))
				{
					if (_xGet(limit.Q3Left) .EqualsWithTol( _xGet(_limit.Q3Left)))
					{
						if (_yGet(limit.Q3Left) .LargerWithTol( _yGet(_limit.Q3Left)))
						{
							_limit.Q3Left = limit.Q3Left;
						}
					}
					else
					{
						_limit.Q3Left = limit.Q3Left;
					}
				}

				if (_yGet(limit.Q1Top) .LargerWithTol( _yGet(_limit.Q1Top)))
				{
					if (_yGet(limit.Q1Top) .EqualsWithTol( _yGet(_limit.Q1Top)))
					{
						if (_xGet(limit.Q1Top) .LargerWithTol( _xGet(_limit.Q1Top)))
						{
							_limit.Q1Top = limit.Q1Top;
						}
					}
					else
					{
						_limit.Q1Top = limit.Q1Top;
					}
				}

                if (_yGet(limit.Q2Top) .LargerWithTol( _yGet(_limit.Q2Top)))
				{
					if (_yGet(limit.Q2Top) .EqualsWithTol( _yGet(_limit.Q2Top)))
					{
						if (_xGet(limit.Q2Top) .SmallerWithTol( _xGet(_limit.Q2Top)))
						{
							_limit.Q2Top = limit.Q2Top;
						}
					}
					else
					{
						_limit.Q2Top = limit.Q2Top;
					}
				}

				if (_yGet(limit.Q3Bottom) .SmallerWithTol( _yGet(_limit.Q3Bottom)))
				{
					if (_yGet(limit.Q3Bottom) .EqualsWithTol( _yGet(_limit.Q3Bottom)))
					{
						if (_xGet(limit.Q3Bottom) .SmallerWithTol( _xGet(_limit.Q3Bottom)))
						{
							_limit.Q3Bottom = limit.Q3Bottom;
						}
					}
					else
					{
						_limit.Q3Bottom = limit.Q3Bottom;
					}
				}

				if (_yGet(limit.Q4Bottom) .SmallerWithTol( _yGet(_limit.Q4Bottom)))
				{
					if (_yGet(limit.Q4Bottom) .EqualsWithTol( _yGet(_limit.Q4Bottom)))
					{
						if (_xGet(limit.Q4Bottom) .LargerWithTol( _xGet(_limit.Q4Bottom)))
						{
							_limit.Q4Bottom = limit.Q4Bottom;
						}
					}
					else
					{
						_limit.Q4Bottom = limit.Q4Bottom;
					}
				}
			}
		}

		// ******************************************************************
		public T[] GetResultsAsArrayOfPoint()
		{
			if (_listOfListOfPoint == null || !_listOfListOfPoint.Any() || !_listOfListOfPoint.First().Any())
			{
				return new T[0];
			}

			int indexQ1Start;
			int indexQ2Start;
			int indexQ3Start;
			int indexQ4Start;
			int indexQ1End;
			int indexQ2End;
			int indexQ3End;
			int indexQ4End;

			PointInfo<T> lastPoint = _q4.HullPoints[_q4.HullPoints.Count - 1];

			if (_q1.HullPoints.Count == 1)
			{
				indexQ1Start = 0;
				indexQ1End = 0;
				lastPoint = _q1.HullPoints[0];
			}
			else
			{
				indexQ1Start = 0;
				indexQ1End = _q1.HullPoints.Count - 1;
				lastPoint = _q1.HullPoints[indexQ1End];
			}

			if (_q2.HullPoints.Count == 1)
			{
				if (_q2.FirstPoint.Equals(lastPoint))
				{
					indexQ2Start = 1;
					indexQ2End = 0;
				}
				else
				{
					indexQ2Start = 0;
					indexQ2End = 0;
					lastPoint = _q2.HullPoints[0];
				}
			}
			else
			{
				if (_q2.FirstPoint.Equals(lastPoint))
				{
					indexQ2Start = 1;
				}
				else
				{
					indexQ2Start = 0;
				}
				indexQ2End = _q2.HullPoints.Count - 1;
				lastPoint = _q2.HullPoints[indexQ2End];
			}

			if (_q3.HullPoints.Count == 1)
			{
				if (_q3.FirstPoint.Equals(lastPoint))
				{
					indexQ3Start = 1;
					indexQ3End = 0;
				}
				else
				{
					indexQ3Start = 0;
					indexQ3End = 0;
					lastPoint = _q3.HullPoints[0];
				}
			}
			else
			{
				if (_q3.FirstPoint.Equals(lastPoint))
				{
					indexQ3Start = 1;
				}
				else
				{
					indexQ3Start = 0;
				}
				indexQ3End = _q3.HullPoints.Count - 1;
				lastPoint = _q3.HullPoints[indexQ3End];
			}

			if (_q4.HullPoints.Count == 1)
			{
				if (_q4.FirstPoint.Equals(lastPoint))
				{
					indexQ4Start = 1;
					indexQ4End = 0;
				}
				else
				{
					indexQ4Start = 0;
					indexQ4End = 0;
				}
			}
			else
			{
				if (_q4.FirstPoint.Equals(lastPoint))
				{
					indexQ4Start = 1;
				}
				else
				{
					indexQ4Start = 0;
				}

				indexQ4End = _q4.HullPoints.Count - 1;
				if (_q4.HullPoints[indexQ4End].Equals(_q1.HullPoints[0]))
				{
					indexQ4End--;
				}
			}


			int countOfFinalHullPoint = (indexQ1End - indexQ1Start) +
										(indexQ2End - indexQ2Start) +
										(indexQ3End - indexQ3Start) +
										(indexQ4End - indexQ4Start) + 4;

			if (countOfFinalHullPoint == 1) // Case where there is only one point or many of only the same point. Auto closed if required.
			{
				return new T[] { _pointConstructor(_q1.HullPoints[0].X, _q1.HullPoints[0].Y) };
			}

			if (_shouldCloseTheGraph)
			{
				countOfFinalHullPoint++;
			}

			T[] results = new T[countOfFinalHullPoint];

			int resIndex = 0;

			for (int n = indexQ1Start; n <= indexQ1End; n++)
			{
				results[resIndex] = _pointConstructor(_q1.HullPoints[n].X, _q1.HullPoints[n].Y);
				resIndex++;
			}

			for (int n = indexQ2Start; n <= indexQ2End; n++)
			{
				results[resIndex] = _pointConstructor(_q2.HullPoints[n].X, _q2.HullPoints[n].Y);
				resIndex++;
			}

			for (int n = indexQ3Start; n <= indexQ3End; n++)
			{
                results[resIndex] = _pointConstructor(_q3.HullPoints[n].X, _q3.HullPoints[n].Y);
				resIndex++;
			}

			for (int n = indexQ4Start; n <= indexQ4End; n++)
			{
                results[resIndex] = _pointConstructor(_q4.HullPoints[n].X, _q4.HullPoints[n].Y);
				resIndex++;
			}

			if (_shouldCloseTheGraph)
			{
                results[resIndex] = _pointConstructor(_q1.FirstPoint.X, _q1.FirstPoint.Y);
			}

			return results;
		}

		// ******************************************************************
		private bool IsZeroData()
		{
			return _listOfListOfPoint == null || !_listOfListOfPoint.Any() || !_listOfListOfPoint.First().Any();
		}

		// ******************************************************************

	}
}

