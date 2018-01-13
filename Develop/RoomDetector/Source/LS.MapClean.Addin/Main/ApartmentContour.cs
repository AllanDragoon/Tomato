using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.Algorithms;

namespace LS.MapClean.Addin.Main
{
    internal class ApartmentContour
    {
        public static IEnumerable<DBPoint> CalcContour(Document doc, IEnumerable<ObjectId> objectIdsList)
        {
            var newCreatedIds = new List<ObjectId>();
            IEnumerable<ObjectId> polygonIds = new List<ObjectId>();
            IEnumerable<ObjectId> splitSourceIds = new List<ObjectId>();
            List<ObjectId> duplicateIds = new List<ObjectId>();

            using (var waitCursor = new WaitCursorSwitcher())
            using (var tolerance = new SafeToleranceOverride())
            {
                // 1. Break down all lines
                doc.Editor.WriteMessage("\n打断所有交叉线...\n");
                var breakCrossingAlgorithm = new BreakCrossingObjectsQuadTree(doc.Editor);
                breakCrossingAlgorithm.Check(objectIdsList);
                var breakIdPairs = breakCrossingAlgorithm.Fix(eraseOld: false).ToList();
                splitSourceIds = breakIdPairs.Select(it => it.Key);
                var checkIds = objectIdsList.Except(splitSourceIds).ToList();
                foreach (var idPair in breakIdPairs)
                {
                    newCreatedIds.AddRange(idPair.Value);
                    checkIds.AddRange(idPair.Value);
                }
                // 2. Erase the duplcate curves
                doc.Editor.WriteMessage("\n排除重复线...\n");
                var duplicateEraserAlgorithm = new DuplicateEntityEraserKdTree(doc.Editor, 0.0005);
                duplicateEraserAlgorithm.Check(checkIds);
                var crossingInfos = duplicateEraserAlgorithm.CrossingInfos;
                if (crossingInfos != null)
                {
                    foreach (var curveCrossingInfo in crossingInfos)
                    {
                        checkIds.Remove(curveCrossingInfo.TargetId);
                        duplicateIds.Add(curveCrossingInfo.TargetId);
                    }

                    // Deal with the source duplicate
                    var groups = crossingInfos.GroupBy(it => it.TargetId);
                    foreach (var g in groups)
                    {
                        if (g.Count() <= 1)
                            continue;
                        bool first = true;
                        foreach (var curveCrossingInfo in g)
                        {
                            if (first)
                            {
                                first = false;
                                continue;
                            }
                            checkIds.Remove(curveCrossingInfo.SourceId);
                            duplicateIds.Add(curveCrossingInfo.SourceId);
                        }
                    }
                }

                // 3. Extend undershoot
                doc.Editor.WriteMessage("\n调整未及点...\n");
                var extendUndershoots = new ExtendUnderShoots(doc.Editor, 0.001);
                extendUndershoots.Check(checkIds);
                var splittedIds = extendUndershoots.Fix(breakTarget: true);
                foreach (var pair in splittedIds)
                {
                    checkIds.Remove(pair.Key);
                    checkIds.AddRange(pair.Value);

                    // Also recorded in newCreatedIds which will be removed at the end.
                    if (newCreatedIds.Contains(pair.Key))
                    {
                        newCreatedIds.Remove(pair.Key);
                        newCreatedIds.AddRange(pair.Value);
                    }

                    polygonIds = checkIds;
                }

                // 2. Make polygons.
                doc.Editor.WriteMessage("开始造闭合多边形...");
                var resultIds = new List<ObjectId>();
                // 3. Union all polygons
                // 4. Get largest are polygon which is the apartment's contour
            }
        }
    }
}
