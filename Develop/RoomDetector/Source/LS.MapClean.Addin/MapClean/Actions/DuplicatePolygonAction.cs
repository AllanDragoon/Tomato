using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DbxUtils.Utils;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class DuplicatePolygonAction : MapCleanActionBase
    {
        public DuplicatePolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.DuplicatePolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
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

            // Check
            editor.WriteMessage("\n开始检查完全重复多边形\n");
            var alogrithm = new PolygonDuplicateSearcher(editor);
            using (var waitCursor = new WaitCursorSwitcher())
            {
                alogrithm.Check(ids);
            }
            var intersects = alogrithm.Intersects;
            if (intersects == null || !intersects.Any())
            {
                editor.WriteMessage("\n检测到0个对象，无需修复\n");
                return false;
            }

            // Ask whether to fix
            var message = String.Format("\n检测到{0}处重复，是否修复?", intersects.Count());
            if (!AskContinueFix(message, editor))
            {
                return false;
            }

            // Delete duplicate entities
            var dictionary = new Dictionary<ObjectId, List<ObjectId>>();
            foreach (var intersect in intersects)
            {
                if (dictionary.ContainsKey(intersect.SourceId))
                {
                    var list = dictionary[intersect.SourceId];
                    if(!list.Contains(intersect.TargetId))
                        list.Add(intersect.TargetId);
                }
                else if (dictionary.ContainsKey(intersect.TargetId))
                {
                    var list = dictionary[intersect.TargetId];
                    if(!list.Contains(intersect.SourceId))
                        list.Add(intersect.SourceId);
                }
                else
                {
                    dictionary[intersect.SourceId] = new List<ObjectId>()
                    {
                        intersect.TargetId
                    };
                }
            }

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var pair in dictionary)
                {
                    var list = pair.Value;
                    foreach (var objId in list)
                    {
                        if(objId.IsErased)
                            continue;
                        var dbObj = transaction.GetObject(objId, OpenMode.ForWrite);
                        dbObj.Erase();
                    }
                }
                transaction.Commit();
            }

            editor.WriteMessage("\n修复完毕!\n");
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
