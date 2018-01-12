using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class SmallPolygonAction : MapCleanActionBase
    {
        public SmallPolygonAction(Document document)
            : base(document)
        {
            Tolerance = 0.05;
        }

        public override ActionType ActionType
        {
            get { return ActionType.SmallPolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<SmallPolygonCheckResult>();
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

                    if (isClosed && curve.Area.Smaller(Tolerance))
                    {
                        var checkResult = new SmallPolygonCheckResult(selectedObjectId);
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
            var smallPolygonCheckResult = checkResult as SmallPolygonCheckResult;
            if (smallPolygonCheckResult == null)
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
            return base.CheckAndFixAllImpl(ids);
        }
    }
}
