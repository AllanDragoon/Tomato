using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class DuplicateEntitiesAction : MapCleanActionBase
    {
        public DuplicateEntitiesAction(Document document)
            : base(document)
        {
            Tolerance = 0.0005;
        }

        public override ActionType ActionType
        {
            get { return ActionType.DeleteDuplicates; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            //var results = new List<DuplicateEntitiesCheckResult>();
            //var editor = Document.Editor;
            //var duplicateEntityEraser = new DuplicateEntityEraser(editor, Tolerance);
            //duplicateEntityEraser.Check();
            //if (duplicateEntityEraser.DuplicateVertexCurveIds == null && duplicateEntityEraser.DuplicateCurveIds == null)
            //    return results;

            //if (duplicateEntityEraser.DuplicateVertexCurveIds != null)
            //    foreach (ObjectId duplicateVertextCurveId in duplicateEntityEraser.DuplicateVertexCurveIds)
            //    {
            //        var checkResult = new DuplicateEntitiesCheckResult(duplicateVertextCurveId);
            //        results.Add(checkResult);
            //    }

            //if (duplicateEntityEraser.DuplicateCurveIds != null)
            //    foreach (var duplicateCurveIds in duplicateEntityEraser.DuplicateCurveIds)
            //    {
            //        var duplicateCurveIdList = duplicateCurveIds.Cast<object>().Cast<ObjectId>().ToList();
            //        foreach (ObjectId objectId in duplicateCurveIdList)
            //        {
            //            var checkResult = new DuplicateEntitiesCheckResult(objectId);
            //            results.Add(checkResult);
            //        }
            //    }

            var results = new List<DuplicateEntitiesCheckResult>();
            var editor = Document.Editor;
            var algorithm = new DuplicateEntityEraserKdTree(editor, Tolerance);
            algorithm.Check(selectedObjectIds);
            if (algorithm.CrossingInfos == null)
                return results;
            foreach (var info in algorithm.CrossingInfos)
            {
                var checkResult = new DuplicateEntitiesCheckResult(info);
                results.Add(checkResult);
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var duplicateEntitiesCheckResult = checkResult as DuplicateEntitiesCheckResult;
            if (duplicateEntitiesCheckResult == null)
                return Status.Rejected;

            // Fix 就是 Erase
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                var crossInfo = duplicateEntitiesCheckResult.CrossingInfo;
                var entity = transaction.GetObject(crossInfo.SourceId, OpenMode.ForWrite);
                entity.Erase();
                checkResult.TargetIds = new ObjectId[]{ crossInfo.TargetId };
                transaction.Commit();
            }
            return Status.Fixed;
        }

        protected override bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids)
        {
            var editor = Document.Editor;
            var duplicateEraserAlgorithm = new DuplicateEntityEraserKdTree(Document.Editor, Tolerance);
            editor.WriteMessage("\n开始检查重复对象...");
            duplicateEraserAlgorithm.Check(ids);
            var crossingInfos = duplicateEraserAlgorithm.CrossingInfos;
            if (crossingInfos == null || !crossingInfos.Any())
            {
                editor.WriteMessage("\n没有检查到重复对象，无需修复\n");
                return true;
            }

            var message = String.Format("\n检查到{0}个重复对象，是否修复？", crossingInfos.Count());
            if (!AcadPromptUtil.AskContinue(message, editor))
                return true;

            editor.WriteMessage("\n开始修复...");
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var curveCrossingInfo in crossingInfos)
                {
                    var targetId = curveCrossingInfo.TargetId;
                    if (targetId.IsErased)
                        continue;
                    var dbObj = transaction.GetObject(targetId, OpenMode.ForWrite);
                    dbObj.Erase();
                    dbObj.Dispose();
                }

                // Deal with the source duplicate
                var groups = crossingInfos.GroupBy(it => it.TargetId);
                foreach (var g in groups)
                {
                    if (g.Count() <= 1)
                        continue;
                    bool first = true;

                    foreach (var curveCrossingInfo in g)
                    {
                        if (first)
                        {
                            first = false;
                            continue;
                        }
                        var sourceId = curveCrossingInfo.SourceId;
                        if (sourceId.IsErased)
                            continue;

                        var dbObj = transaction.GetObject(sourceId, OpenMode.ForWrite);
                        dbObj.Erase();
                        dbObj.Dispose();
                    }
                }
                transaction.Commit();
            }
            editor.WriteMessage("\n修复所有重复对象成功！\n");
            return true;
        }

        public bool CheckVertex { get; set; }
        public bool CheckCurve { get; set; }
    }
}
