using System;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Utils
{
    public class SafeToleranceOverride : IDisposable
    {
        private Tolerance _mOldTolerance;
        private const double _equalVectorTolerance = 0.00005;
        private const double _equalPointTolerance = 0.00005;

        public SafeToleranceOverride(double? newPointTol = null, double? newVecTol = null)
        {
            _mOldTolerance = Tolerance.Global;

            var pointTolerance = _equalPointTolerance;
            if (newPointTol != null)
                pointTolerance = newPointTol.Value;
            var vecTolerance = _equalVectorTolerance;
            if (newVecTol != null)
                vecTolerance = newVecTol.Value;

            Tolerance.Global = new Tolerance(pointTolerance, vecTolerance);
        }

        public void Dispose()
        {
            Tolerance.Global = _mOldTolerance;
        }
    }
}
