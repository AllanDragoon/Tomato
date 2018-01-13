using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;
using TopologyTools.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class IntersectPolygonAction : MapCleanActionBase
    {
        public IntersectPolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.IntersectPolygon; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var result = new List<IntersectPolygonCheckResult>();
            //var polygonOverlaps = OverlapPolygonDetector.FindPolygonOverlaps(selectedObjectIds.ToArray());
            //foreach (var overlap in polygonOverlaps.GeometryOverlaps)
            //{
            //    var checkResult = new IntersectPolygonCheckResult(overlap);
            //    result.Add(checkResult);
            //}
            var algorithm = new PolygonIntersectWithoutHoleSearcher(Document.Editor);

            algorithm.Check(selectedObjectIds);
            var intersects = algorithm.Intersects;
            if (intersects == null)
                return result;

            foreach (var polygonIntersect in intersects)
            {
                var checkResult = new IntersectPolygonCheckResult(polygonIntersect);
                result.Add(checkResult);
            }

            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            // Do nothing
            return Status.NoFixMethod;
        }
    }
}
