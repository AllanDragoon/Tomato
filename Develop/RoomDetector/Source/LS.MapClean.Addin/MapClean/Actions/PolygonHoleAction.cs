using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean.Actions
{
    public class PolygonHoleAction : MapCleanActionBase
    {
        public PolygonHoleAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.PolygonHole; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var algorithm = new PolygonHoleSearcher(Document.Editor);
            algorithm.Check(selectedObjectIds);
            var result = new List<PolygonHoleCheckResult>();
            foreach (var hole in algorithm.Holes)
            {
                var checkResult = new PolygonHoleCheckResult(hole);
                result.Add(checkResult);
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            // Do nothing
            return Status.NoFixMethod;
        }
    }
}
