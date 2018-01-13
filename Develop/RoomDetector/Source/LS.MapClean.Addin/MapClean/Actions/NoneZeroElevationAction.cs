using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class NoneZeroElevationAction : MapCleanActionBase
    {
        public NoneZeroElevationAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.NoneZeroElevation; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            // Do nothing
            return new List<CheckResult>();
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            // Do nothing
            return Status.NoFixMethod;
        }

        protected override bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids)
        {
            var editor = Document.Editor;
            var database = Document.Database;

            // Check
            editor.WriteMessage("\n开始检查高程不为0对象...");
            var elevationIds = new List<ObjectId>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var modelspace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
                foreach (ObjectId objectId in modelspace)
                {
                    if (CurveUtils.IsCurveNonZeroElevation(objectId, transaction))
                    {
                        elevationIds.Add(objectId);
                    }
                }
                transaction.Commit();
            }

            // Fix
            if (elevationIds.Count <= 0)
            {
                editor.WriteMessage("\n检测到0个对象，无需修复\n");
                return false;
            }

            // Ask whether to fix.
            var message = String.Format("\n检测到{0}个对象，是否修复?", elevationIds.Count);
            if (!AskContinueFix(message, editor))
            {
                return false;
            }

            var failedIds = new List<ObjectId>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                using (var progresser = new SimpleLongOperationManager(String.Empty))
                {
                    progresser.SetTotalOperations(elevationIds.Count);
                    foreach (ObjectId elevationId in elevationIds)
                    {
                        var result = CurveUtils.SetCurveElevationToZero(transaction, elevationId);
                        if (!result)
                            failedIds.Add(elevationId);
                        progresser.Tick();
                    }
                }
                transaction.Commit();
            }

            editor.WriteMessage("\n修复完毕：成功{0}个，失败{1}个\n", elevationIds.Count - failedIds.Count, failedIds.Count);
            return true;
        }

        protected bool AskContinueFix(string message, Editor editor)
        {
            var options = AcadPromptUtil.CreatePromptOptions<PromptKeywordOptions>(message, new string[] { "Yes", "No" },
                new string[] { "是", "否" }, new char[] { 'Y', 'N' });
            options.AllowNone = true;
            PromptResult promptResult = null;
            do
            {
                promptResult = editor.GetKeywords(options);
            } while (promptResult.Status != PromptStatus.OK
                && promptResult.Status != PromptStatus.Cancel
                && promptResult.Status != PromptStatus.None);

            if (promptResult.Status == PromptStatus.Cancel)
                return false;

            if (promptResult.Status == PromptStatus.OK && promptResult.StringResult == "No")
                return false;

            return true;
        }
    }
}
