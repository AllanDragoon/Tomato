using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class ResolveShortLinesAction : MapCleanActionBase
    {
        public ResolveShortLinesAction(Document document)
            : base(document)
        {
            Tolerance = 0.2;
        }

        public override ActionType ActionType
        {
            get { return ActionType.EraseShort; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<ResolveShortLineCheckResult>();
            var editor = Document.Editor;
            var shortLineEraser = new ShortLineEraser(editor, Tolerance);
            shortLineEraser.Check(selectedObjectIds);
            if (shortLineEraser.ShortLineObjectIdCollection == null)
                return results;

            foreach (ObjectId shortLineObjectId in shortLineEraser.ShortLineObjectIdCollection)
            {
                var checkResult = new ResolveShortLineCheckResult(shortLineObjectId);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var resolveShortLineCheckResult = checkResult as ResolveShortLineCheckResult;
            if (resolveShortLineCheckResult == null)
                return Status.Rejected;

            // Fix 就是 Erase
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var sourceId in checkResult.SourceIds)
                {
                    var entity = transaction.GetObject(sourceId, OpenMode.ForWrite);
                    entity.Erase();
                }
                transaction.Commit();
            }
            return Status.Fixed;
        }
    }
}
