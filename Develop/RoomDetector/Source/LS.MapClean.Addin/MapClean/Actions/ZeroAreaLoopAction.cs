using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class ZeroAreaLoopAction : MapCleanActionBase
    {
        public ZeroAreaLoopAction(Document document)
            : base(document)
        {
            
        }

        public override ActionType ActionType
        {
            get { return ActionType.ZeroAreaLoop; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<ZeroAreaLoopCheckResult>();
            var database = Document.Database;
            using (var switcher = new SafeToleranceOverride())
            {
                using (var transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (var selectedObjectId in selectedObjectIds)
                    {
                        var dbObj = transaction.GetObject(selectedObjectId, OpenMode.ForRead);
                        var curve = dbObj as Curve;
                        if (curve == null || curve is Xline) // Xline will cause exception
                            continue;

                        bool isClosed = false;
                        var polyline = curve as Polyline;
                        var polyline2d = curve as Polyline2d;
                        if (polyline != null)
                            isClosed = polyline.Closed;
                        else if (polyline2d != null)
                            isClosed = polyline2d.Closed;

                        if (!isClosed)
                        {
                            var startPoint = curve.StartPoint;
                            var endPoint = curve.EndPoint;
                            if (startPoint.IsEqualTo(endPoint))
                                isClosed = true;
                        }

                        if (isClosed && curve.Area.EqualsWithTolerance(0.0, 0.00001))
                        {
                            var checkResult = new ZeroAreaLoopCheckResult(selectedObjectId, curve.StartPoint);
                            results.Add(checkResult);
                        }
                    }
                    transaction.Commit();
                }
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var zeroAreaCheckResult = checkResult as ZeroAreaLoopCheckResult;
            if (zeroAreaCheckResult == null)
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
    }
}
