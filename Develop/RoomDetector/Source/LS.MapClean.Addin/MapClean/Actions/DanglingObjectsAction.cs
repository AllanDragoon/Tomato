using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class DanglingObjectsAction : MapCleanActionBase
    {
        public DanglingObjectsAction(Document document)
            : base(document)
        {
            // Set default tolerance.
            Tolerance = 50;
        }

        public override ActionType ActionType
        {
            get { return ActionType.EraseDangling; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<DanglingCheckResult>();
            var editor = Document.Editor;
            var danglingEraser = new DanglingEraser(editor, Tolerance);
            danglingEraser.Check(selectedObjectIds);
            if (danglingEraser.DanglingPaths == null)
                return results;

            // Convert each dangling path to DanglingCheckResult.
            foreach (var path in danglingEraser.DanglingPaths)
            {
                // Some dangling path is not a valid path if its source vertex and target vertex's Id are not same.
                var checkResult = new DanglingCheckResult(path);
                if (checkResult.SourceIds.Any())
                    results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var danglingCheckResult = checkResult as DanglingCheckResult;
            if (danglingCheckResult == null)
                return Status.Rejected;

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

        protected override bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids)
        {
            var checkResults = Check(ids);
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var checkResult in checkResults)
                {
                    foreach (var sourceId in checkResult.SourceIds)
                    {
                        var entity = transaction.GetObject(sourceId, OpenMode.ForWrite);
                        entity.Erase();
                    }
                }
                transaction.Commit();
            }
            return true;
        }
    }
}
