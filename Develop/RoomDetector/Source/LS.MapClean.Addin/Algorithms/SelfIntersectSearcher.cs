using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LS.MapClean.Addin.Utils;

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

            // 调低计算精度，否则有些交叉因为精度问题算不出来
            var oldTolerance = DoubleExtensions.STolerance;
            DoubleExtensions.STolerance = 1e-04;
            var watch = Stopwatch.StartNew();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var intersects = new List<CurveCrossingInfo>();
                foreach (var selectedObjectId in selectedObjectIds)
                {
                    //var bspBuilder = new Curve2dBspBuilder(new ObjectId[] {selectedObjectId}, transaction);
                    //IEnumerable<CurveCrossingInfo> duplicateEntities = null;
                    //var crossingInfos = bspBuilder.SearchRealIntersections(true, out duplicateEntities);
                    //intersects.AddRange(crossingInfos);
                    var algorithm = new BreakCrossingObjectsQuadTree(Editor);
                    algorithm.Check(new ObjectId[] { selectedObjectId });
                    if (algorithm.CrossingInfos != null && algorithm.CrossingInfos.Any())
                    {
                        intersects.AddRange(algorithm.CrossingInfos);
                    }
                }

                _selfIntersects = intersects;
                transaction.Commit();
            }

            // 恢复默认的计算精度值
            DoubleExtensions.STolerance = oldTolerance;
            watch.Stop();
            var elapseMs = watch.ElapsedMilliseconds;
#if DEBUG
            System.Diagnostics.Debug.WriteLine("查找自相交花费{0}毫秒", elapseMs);
#endif
        }
    }
}
