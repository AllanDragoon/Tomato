using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class ExtendUndershootsAction : MapCleanActionBase
    {
        public ExtendUndershootsAction(Document document)
            : base(document)
        {
            // Temporarily
            Tolerance = 2.0;
        }

        public override ActionType ActionType
        {
            get { return ActionType.ExtendUndershoots; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        private bool _breakTargetCurve = true;
        public bool BreakTargetCurve
        {
            get { return _breakTargetCurve; }
            set { _breakTargetCurve = value; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<UnderShootCheckResult>();
            var editor = Document.Editor;
            var underShoots = new ExtendUnderShoots(editor, Tolerance);
            underShoots.Check(selectedObjectIds);
            if (underShoots.UnderShootInfos == null)
                return results;

            foreach (var undershoot in underShoots.UnderShootInfos)
            {
                var checkResult = new UnderShootCheckResult(undershoot);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var undershootCheckResult = checkResult as UnderShootCheckResult;
            if (undershootCheckResult == null)
                return Status.Rejected;

            var intersection = undershootCheckResult.IntersectionInfo;
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                // Extend source curve
                CurveUtils.ExtendCurve(intersection.SourceId, intersection.IntersectPoint, intersection.SourceExtendType, transaction);
                resultIds.Add(intersection.SourceId);

                // Break target curve
                if (BreakTargetCurve)
                {
                    var blockTable = (BlockTable)transaction.GetObject(Document.Database.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var splittedCurves = CurveUtils.SplitCurve(intersection.TargetId, new Point3d[] {intersection.IntersectPoint}, transaction);
                    if (splittedCurves != null && splittedCurves.Count > 0)
                    {
                        foreach (DBObject dbObj in splittedCurves)
                        {
                            var splitedCurve = dbObj as Entity;
                            if (splitedCurve == null)
                                continue;

                            var objId = modelSpace.AppendEntity(splitedCurve);
                            transaction.AddNewlyCreatedDBObject(splitedCurve, true);
                            resultIds.Add(objId);
                        }

                        // Erase the original one
                        var sourceCurve = (Entity)transaction.GetObject(intersection.TargetId, OpenMode.ForRead) as Curve;
                        if (sourceCurve != null)
                        {
                            sourceCurve.UpgradeOpen();
                            sourceCurve.Erase();
                        }
                    }
                }
                transaction.Commit();
            }
            return Status.Fixed;
        }
    }
}
