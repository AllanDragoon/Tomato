using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean.Actions
{
    public class SharpCornerAction : MapCleanActionBase
    {
        public SharpCornerAction(Document document)
            : base(document)
        {
            Tolerance = 5.0;
        }

        public override ActionType ActionType
        {
            get { return ActionType.SharpCornerPolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var result = new List<SharpCornerCheckResult>();
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return result;

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in selectedObjectIds)
                {
                    var points = CheckSharpCorners(objectId, transaction);
                    if (points.Any())
                    {
                        result.Add(new SharpCornerCheckResult(objectId, points));
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        private IEnumerable<Point3d> CheckSharpCorners(ObjectId curveId, Transaction transaction)
        {
            var result = new List<Point3d>();
            var distinctVertices = CurveUtils.GetDistinctVertices(curveId, transaction);
            // 保证首尾点不相同
            if(distinctVertices[0] == distinctVertices[distinctVertices.Count - 1])
                distinctVertices.RemoveAt(distinctVertices.Count - 1);
            for (int i = 0; i < distinctVertices.Count; i++)
            {
                var current = distinctVertices[i];
                var prev = distinctVertices[(i - 1 + distinctVertices.Count)%distinctVertices.Count];
                var next = distinctVertices[(i + 1)%distinctVertices.Count];
                var prevDir = prev - current;
                var nextDir = next - current;
                var angle = prevDir.GetAngleTo(nextDir);
                if(angle.SmallerOrEqual(Tolerance/180.0 * Math.PI))
                    result.Add(current);
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            // 不提供修复功能
            return Status.NoFixMethod;
        }
    }
}
