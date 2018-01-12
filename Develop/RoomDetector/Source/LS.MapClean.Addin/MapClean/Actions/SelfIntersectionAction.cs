using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class SelfIntersectionAction : MapCleanActionBase
    {
        public SelfIntersectionAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.SelfIntersect; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var result = new List<SelfIntersectCheckResult>();
            foreach (var objId in selectedObjectIds)
            {
                try
                {
                    var algorithm = new SelfIntersectSearcher(Document.Editor);
                    algorithm.Check(new ObjectId[] { objId });
                    if (algorithm.SelfIntersects != null)
                    {
                        foreach (var crossingInfo in algorithm.SelfIntersects)
                        {
                            var checkResult = new SelfIntersectCheckResult(crossingInfo);
                            result.Add(checkResult);
                        }
                    }
                }
                catch (Exception)
                {
                }
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
