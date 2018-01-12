using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;
using QuickGraph;

namespace LS.MapClean.Addin.MapClean
{
    public class ApparentIntersectionAction : MapCleanActionBase
    {
        public ApparentIntersectionAction(Document document)
            : base(document)
        {
            // Temporarily
            Tolerance = 2.0;
        }

        public override ActionType ActionType
        {
            get { return ActionType.ApparentIntersection; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<ApparentIntersectionCheckResult>();
            var editor = Document.Editor;
            var checker = new ApparentIntersectionFixer(editor, Tolerance);
            checker.Check(selectedObjectIds);
            if (checker.ApparentIntersections == null)
                return results;

            // Convert each apparent intersection to ApparentIntersectionCheckResult
            foreach (var intersection in checker.ApparentIntersections)
            {
                var checkResult = new ApparentIntersectionCheckResult(intersection);
                results.Add(checkResult);
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var apparentIntersectionCheckResult = checkResult as ApparentIntersectionCheckResult;
            if (apparentIntersectionCheckResult == null)
                return Status.Rejected;

            var intersection = apparentIntersectionCheckResult.IntersectionInfo;
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                CurveUtils.ExtendCurve(intersection.SourceId, intersection.IntersectPoint, intersection.SourceExtendType, transaction);
                CurveUtils.ExtendCurve(intersection.TargetId, intersection.IntersectPoint, intersection.TargetExtendType, transaction);
                
                // Add sourceId and targetId in the resultIds.
                resultIds.Add(intersection.SourceId);
                resultIds.Add(intersection.TargetId);

                // Target curve
                transaction.Commit();
            }
            return Status.Fixed;
        }

    }
}
