using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean.Actions
{
    public class AntiClockwisePolygonAction : MapCleanActionBase
    {
        public AntiClockwisePolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.AntiClockwisePolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<Autodesk.AutoCAD.DatabaseServices.ObjectId> selectedObjectIds)
        {
            var result = new List<AntiClockwisePolygonCheckResult>();
            if (!selectedObjectIds.Any())
                return result;

            var database = selectedObjectIds.First().Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in selectedObjectIds)
                {
                    var pline = tr.GetObject(objectId, OpenMode.ForRead);
                    if (pline == null)
                        continue;

                    bool bIsCloseWise = false;
                    if (pline is Polyline)
                    {
                        bIsCloseWise = CurveUtils.IsPolygonClockWise(pline as Polyline);
                    }
                    else if (pline is Polyline2d)
                    {
                        bIsCloseWise = CurveUtils.IsPolygonClockWise(pline as Polyline2d, tr);
                    }
                    else
                    {
                        continue;
                    }
                    
                    if (!bIsCloseWise){
                        result.Add(new AntiClockwisePolygonCheckResult(objectId));
                    }
                }

                tr.Commit();
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<Autodesk.AutoCAD.DatabaseServices.ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();

            var antiClockWiseCheckResult = checkResult as AntiClockwisePolygonCheckResult;
            if (antiClockWiseCheckResult == null)
                return Status.Failed;

            var curveId = antiClockWiseCheckResult.SourceIds.First();
            var sucess = Status.Fixed;
            using (var tr = curveId.Database.TransactionManager.StartTransaction())
            {
                var curve = tr.GetObject(curveId, OpenMode.ForWrite) as Curve;
                if (curve != null)
                {
                    try
                    {
                        curve.ReverseCurve();
                    }
                    catch (Exception)
                    {
                        sucess = Status.Failed;
                    }
                }
                tr.Commit();
            }

            resultIds.Add(curveId);
            return sucess;
        }
    }
}
