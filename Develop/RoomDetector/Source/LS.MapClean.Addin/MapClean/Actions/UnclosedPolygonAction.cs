using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace LS.MapClean.Addin.MapClean
{
    public class UnclosedPolygonAction : MapCleanActionBase
    {
        public UnclosedPolygonAction(Document document)
            : base(document)
        {
            
        }

        public override ActionType ActionType
        {
            get { return ActionType.UnclosedPolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<UnclosedPolygonCheckResult>();
            var database = Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var selectedObjectId in selectedObjectIds)
                {
                    var dbObj = transaction.GetObject(selectedObjectId, OpenMode.ForRead);
                    var curve = dbObj as Curve;
                    if (curve == null)
                        continue;

                    bool isClosed = false;
                    var polyline = curve as Polyline;
                    var polyline2d = curve as Polyline2d;
                    if (polyline == null && polyline2d == null)
                        continue;

                    if (polyline != null)
                        isClosed = polyline.Closed;
                    else if (polyline2d != null)
                        isClosed = polyline2d.Closed;

                    //if (!isClosed)
                    //{
                    //    var startPoint = curve.StartPoint;
                    //    var endPoint = curve.EndPoint;
                    //    if (startPoint.IsEqualTo(endPoint))
                    //        isClosed = true;
                    //}

                    if (!isClosed)
                    {
                        var checkResult = new UnclosedPolygonCheckResult(selectedObjectId);
                        results.Add(checkResult);
                    }
                }
                transaction.Commit();
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var unclosedPolygonCheckResult = checkResult as UnclosedPolygonCheckResult;
            if (unclosedPolygonCheckResult == null)
                return Status.Rejected;

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                var sourceId = checkResult.SourceIds.First();
                var dbObj = transaction.GetObject(sourceId, OpenMode.ForWrite);
                var curve = dbObj as Curve;
                if (curve == null)
                    return Status.Rejected;

                var polyline = curve as Polyline;
                var polyline2d = curve as Polyline2d;
                if (polyline != null)
                    polyline.Closed = true;
                else if (polyline2d != null)
                    polyline2d.Closed = true;
                transaction.Commit();
            }
            return Status.Fixed;
        }
    }
}
