using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class SmallPolygonGapAction : MapCleanActionBase
    {
        public SmallPolygonGapAction(Document document, double tolerance = 0.2)
            : base(document)
        {
            Tolerance = tolerance;
        }

        public override ActionType ActionType
        {
            get { return ActionType.SmallPolygonGap; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var algorithm = new PolygonGapSearcherKdTree(Document.Editor, Tolerance);
            algorithm.Check(selectedObjectIds);
            var result = new List<SmallPolygonGapCheckResult>();
            foreach (var gap in algorithm.Gaps)
            {
                var checkResult = new SmallPolygonGapCheckResult(gap);
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
