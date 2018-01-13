using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class RectifyPointDeviationAction : MapCleanActionBase
    {
        public RectifyPointDeviationAction(Document document)
            : base(document)
        {
            Tolerance = 0.005;
        }

        public override ActionType ActionType
        {
            get { return ActionType.RectifyPointDeviation; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<NearVerticesCheckResult>();
            var algorithm = new NearVerticesRectifier(Document.Database, Tolerance);
            algorithm.Check(selectedObjectIds);
            foreach (var nears in algorithm.NearVertices)
            {
                var checkResult = new NearVerticesCheckResult(nears);
                results.Add(checkResult);
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var cr = checkResult as NearVerticesCheckResult;
            if (cr == null)
                return Status.Invalid;
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                NearVerticesRectifier.RectifyNearVertices(cr.NearVertices, transaction);
                resultIds.AddRange(cr.SourceIds);
                transaction.Commit();
            }
            return Status.Fixed;
        }
    }
}
