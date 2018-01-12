using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class ZeroLengthObjectsAction : MapCleanActionBase
    {
        public ZeroLengthObjectsAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.ZeroLength; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<ZeroLengthCheckResult>();
            var editor = Document.Editor;
            var zeroLengthEraser = new ZeroLengthEraser(editor);
            zeroLengthEraser.Check(selectedObjectIds);
            if (zeroLengthEraser.ZerolengthObjectIdCollection == null)
                return results;

            foreach (ObjectId zeroLengthObjectId in zeroLengthEraser.ZerolengthObjectIdCollection)
            {
                var checkResult = new ZeroLengthCheckResult(zeroLengthObjectId);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var zeroLengthCheckResult = checkResult as ZeroLengthCheckResult;
            if (zeroLengthCheckResult == null)
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
