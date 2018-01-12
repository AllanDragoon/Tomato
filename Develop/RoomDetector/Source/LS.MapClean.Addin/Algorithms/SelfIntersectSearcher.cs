using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace LS.MapClean.Addin.Algorithms
{
    public class SelfIntersectSearcher : AlgorithmWithEditor
    {
        private IEnumerable<CurveCrossingInfo> _selfIntersects = new CurveCrossingInfo[0];
        public IEnumerable<CurveCrossingInfo> SelfIntersects
        {
            get { return _selfIntersects; }
        }

        public SelfIntersectSearcher(Editor editor)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var watch = Stopwatch.StartNew();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var intersects = new List<CurveCrossingInfo>();
                foreach (var selectedObjectId in selectedObjectIds)
                {
                    var bspBuilder = new Curve2dBspBuilder(new ObjectId[] {selectedObjectId}, transaction);
                    IEnumerable<CurveCrossingInfo> duplicateEntities = null;
                    var crossingInfos = bspBuilder.SearchRealIntersections(true, out duplicateEntities);
                    intersects.AddRange(crossingInfos);
                }

                _selfIntersects = intersects;
                transaction.Commit();
            }
            watch.Stop();
            var elapseMs = watch.ElapsedMilliseconds;
#if DEBUG
            System.Diagnostics.Debug.WriteLine("查找自相交花费{0}毫秒", elapseMs);
#endif
        }
    }
}
