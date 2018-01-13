using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class BreakCrossingObjectsAction : MapCleanActionBase
    {
        public BreakCrossingObjectsAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.BreakCrossing; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            //var results = new List<CrossingCheckResult>();
            //var editor = Document.Editor;
            //var crossingObjects = new BreakCrossingObjects(editor, Tolerance);
            //crossingObjects.Check();
            //if (crossingObjects.CrossingPoints == null)
            //    return results;

            //foreach (var crossingPoint in crossingObjects.CrossingPoints)
            //{
            //    var checkResult = new CrossingCheckResult(crossingPoint);
            //    results.Add(checkResult);
            //}

            //return results;

            var editor = Document.Editor;
            var algorithm = new BreakCrossingObjectsQuadTree(editor);
            algorithm.Check(selectedObjectIds);
            var result = new List<CrossingCheckResult>();
            foreach (var crossingInfo in algorithm.CrossingInfos)
            {
                var checkResult = new CrossingCheckResult(crossingInfo);
                result.Add(checkResult);
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var crossingCheckResult = checkResult as CrossingCheckResult;
            if (crossingCheckResult == null)
                return Status.Rejected;

            var crossingInfo = crossingCheckResult.CrossingInfo;
            var distinctIds = new HashSet<ObjectId>();
            distinctIds.Add(crossingInfo.SourceId);
            distinctIds.Add(crossingInfo.TargetId);

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                //// distinctIds == 1说明是自交线
                bool isSelfIntersection = (distinctIds.Count == 1);

                var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(Document.Database), OpenMode.ForWrite);
                foreach (var sourceId in distinctIds)
                {
                    var sourceCurve = transaction.GetObject(sourceId, OpenMode.ForWrite) as Curve;
                    DBObjectCollection allSplitCurves = null;
                    if (isSelfIntersection)
                    {
                        allSplitCurves = CurveUtils.SplitSelfIntersectCurve(sourceCurve, crossingInfo.IntersectPoints, transaction);
                    }
                    else // Use CurveUtils.SplitCurve take less time.
                    {
                        allSplitCurves = CurveUtils.SplitCurve(sourceCurve, crossingInfo.IntersectPoints);
                    }

                    // The splitted curves has the same layer with original curve, 
                    // so we needn't set its layer explicitly.
                    foreach (Curve splitCurve in allSplitCurves)
                    {
                        var curveId = modelSpace.AppendEntity(splitCurve);
                        transaction.AddNewlyCreatedDBObject(splitCurve, true);
                        // Add splited curve to resultIds.
                        resultIds.Add(curveId);
                    }

                    if (allSplitCurves.Count > 0)
                    {
                        // Erase the old one
                        sourceCurve.Erase();
                    }
                }
                transaction.Commit();
            }
            return Status.Fixed;
        }

        protected override bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids)
        {
            var editor = Document.Editor;
            var algorithm = new BreakCrossingObjectsQuadTree(editor);

            var checkedIds = ids;
            var first = true;
            var lastCount = 0;
            using(var waitCursor = new WaitCursorSwitcher())
            while (true)
            {
                var checkMessage = String.Format("\n{0}检查交叉对象...", first?"开始":"继续");
                if (first)
                    first = false;
                editor.WriteMessage(checkMessage);

                algorithm.Check(checkedIds);
                var count = algorithm.CrossingInfos.Count();
                if (count == 0 || count == lastCount)
                {
                    editor.WriteMessage("\n检测到0处交叉，无需修复\n");
                    editor.WriteMessage("\n提示：在删除重复对象之后，请再执行一次打断交叉对象，保证图形能够完全清理成功\n");
                    return true;
                }

                lastCount = count;
                var message = String.Format("\n检测到{0}处交叉，是否打断？", count);
                if (!AcadPromptUtil.AskContinue(message, editor))
                    return true;

                editor.WriteMessage("\n开始打断...");
                var breakIdPairs = algorithm.Fix(eraseOld: true);
                
                // Need recheck
                // TODO: this is a temporary solution, will be modified for the new checked Ids set.
                checkedIds = MapCleanService.Instance.PrepareObjectIdsForCheck();
            }
        }
    }
}
