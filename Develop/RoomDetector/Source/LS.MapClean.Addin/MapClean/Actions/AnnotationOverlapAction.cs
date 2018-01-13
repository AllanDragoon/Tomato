using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.MapClean
{
    public class AnnotationOverlapAction : MapCleanActionBase
    {
        public AnnotationOverlapAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.AnnotationOverlap; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<Autodesk.AutoCAD.DatabaseServices.ObjectId> selectedObjectIds)
        {
            var result = new List<AnnotationOverlapCheckResult>();
            var algorithm = new PolygonIntersectSearcher(Document.Editor, null);
            algorithm.Check(selectedObjectIds);
            var intersects = algorithm.Intersects;
            if (intersects == null)
                return result;

            foreach (var polygonIntersect in intersects)
            {
                var checkResult = new AnnotationOverlapCheckResult(polygonIntersect);
                result.Add(checkResult);
                // Dispose the intersection polylines
                foreach (var polyline in polygonIntersect.Intersections)
                {
                    polyline.Dispose();
                }
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<Autodesk.AutoCAD.DatabaseServices.ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            return Status.NoFixMethod;
        }
    }
}
