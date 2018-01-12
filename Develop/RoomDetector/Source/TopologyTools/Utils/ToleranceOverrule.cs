using System;
using Autodesk.AutoCAD.Geometry;

namespace TopologyTools.Utils
{
    public class ToleranceOverrule : IDisposable
    {
        private Tolerance _mOldTolerance;
        private const double SaferTolerance = 1E-5;

        public ToleranceOverrule(double? tolerance)
        {
            _mOldTolerance = Tolerance.Global;
            double newTolerance = SaferTolerance;
            if (tolerance != null)
                newTolerance = tolerance.Value;
            Tolerance.Global = new Tolerance(newTolerance, newTolerance);
        }

        public void Dispose()
        {
            Tolerance.Global = _mOldTolerance;
        }
    }
}
