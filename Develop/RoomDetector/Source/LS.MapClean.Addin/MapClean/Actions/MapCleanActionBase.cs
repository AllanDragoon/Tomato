using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public abstract class MapCleanActionBase
    {
        protected MapCleanActionBase(Document document)
        {
            Document = document;
        }

        /// <summary>
        /// Related document.
        /// </summary>
        public Document Document { get; private set; }

        public abstract ActionType ActionType { get; }

        public abstract bool Hasparameters { get; }

        public double Tolerance { get; set; }

        public IEnumerable<CheckResult> Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();

            IEnumerable<CheckResult> result = null;
            using(var waitCursor = new WaitCursorSwitcher())
            using (var switcher = new SafeToleranceOverride())
            {
                result = CheckImpl(selectedObjectIds);
            }

            watch.Stop();
            var elapseMs = watch.ElapsedMilliseconds;
            if (Document != null)
            {
                Document.Editor.WriteMessage("\n本次检查用时{0}毫秒\n", elapseMs);
            }

            return result;
        }
        protected abstract IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds);

        public MapClean.Status Fix(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            using (var siwtcher = new SafeToleranceOverride())
            {
                return FixImpl(checkResult, out resultIds);
            }
        }

        protected abstract MapClean.Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds);

        public bool CheckAndFixAll(IEnumerable<ObjectId> ids)
        {
            using (var switcher = new SafeToleranceOverride())
            {
                return CheckAndFixAllImpl(ids);
            }
        }

        protected virtual bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids)
        {
            // Do nothing by default.
            return true;
        }
    }
}
