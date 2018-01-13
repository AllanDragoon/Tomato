using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.Algorithms;
using NetTopologySuite.Geometries;
using TopologyTools.Utils;

namespace LS.MapClean.Addin.Main
{
    internal class ApartmentContour
    {
        public static List<Point2d> CalcContour(Document doc, IEnumerable<ObjectId> objectIdsList)
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
                }

                // 4. 排除0面积闭合线
                doc.Editor.WriteMessage("\n排除零面积闭合线...\n");
                var zeroAreaLoopIds = CurveUtils.GetZeroAreaLoop(checkIds, doc.Database);
                foreach (var zeroAreaLoopId in zeroAreaLoopIds)
                {
                    checkIds.Remove(zeroAreaLoopId);
                    duplicateIds.Add(zeroAreaLoopId);
                }

                // 5. 删除0长度对象
                doc.Editor.WriteMessage("\n排除零长度对象...\n");
                var zeroLengthEraser = new ZeroLengthEraser(doc.Editor);
                zeroLengthEraser.Check(checkIds);
                foreach (ObjectId zeroLengthId in zeroLengthEraser.ZerolengthObjectIdCollection)
                {
                    checkIds.Remove(zeroLengthId);
                    duplicateIds.Add(zeroLengthId);
                }

                polygonIds = checkIds;

                //// Test code
                //using (var transaction = doc.Database.TransactionManager.StartTransaction())
                //{
                //    var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                //    ObjectId layerId = LayerUtils.AddNewLayer(doc.Database, "temp-poly", "Continuous", color);
                //    foreach (var polygonId in polygonIds)
                //    {
                //        var entity = (Entity)transaction.GetObject(polygonId, OpenMode.ForWrite);
                //        entity.Color = color;
                //        entity.LayerId = layerId;
                //    }
                //    transaction.Commit();
                //}
                //return new List<Point2d>();
            }

            // 2. Make polygons.
            doc.Editor.WriteMessage("开始造闭合多边形...");
            var resultIds = new List<ObjectId>();
            if (polygonIds.Any())
            {
                var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                ObjectId layerId = LayerUtils.AddNewLayer(doc.Database, "temp-poly", "Continuous", color);

                using (var tolerance = new SafeToleranceOverride())
                using (var waitCursor = new WaitCursorSwitcher())
                {
                    //var polygons = MinimalLoopSearcher2.Search(polygonIds, doc);
                    //using (var transaction = doc.Database.TransactionManager.StartTransaction())
                    //{
                    //    var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                    //    var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);
                    //    foreach (var polyline in polygons)
                    //    {
                    //        polyline.Color = color;
                    //        polyline.LayerId = layerId;
                    //        var id = modelspace.AppendEntity(polyline);
                    //        resultIds.Add(id);
                    //        transaction.AddNewlyCreatedDBObject(polyline, true);
                    //    }
                    //    transaction.Commit();
                    //}
                    resultIds = NtsUtils.PolygonizeLineStrings(doc.Database, polygonIds, "temp-poly", color, 0.0001);
                }
            }

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var newCreatedId in newCreatedIds)
                {
                    if (newCreatedId.IsErased)
                        continue;
                    var dbObj = tr.GetObject(newCreatedId, OpenMode.ForWrite);
                    dbObj.Erase();
                }

                ////////////////////////////////////////////////////////////////////////////////////////
                tr.Commit();
            }
            doc.Editor.WriteMessage("造多边形成功！");
            // 3. Union all polygons
            var database = doc.Database;
            var partitioner = new DrawingPartitioner(database);
            partitioner.Check(resultIds);
            // 4. Get largest are polygon which is the apartment's contour
            var polylines = new List<Polyline>();
            foreach (var region in partitioner.IsolatedRegions)
            {
                var polyline = new Polyline(region.Contour.Count);
                int i = 0;
                foreach (var point in region.Contour)
                {
                    polyline.AddVertexAt(i++, new Point2d(point.X, point.Y), 0, 0, 0);
                }
                polyline.Closed = true;
                polylines.Add(polyline);
            }
            polylines.Sort((poly1, poly2) =>
            {
                if (poly1.Area > poly2.Area)
                    return -1;
                return 1;
            });
            Polyline largestPolyline = polylines.FirstOrDefault();
            var resultPoints = new List<Point2d>();
            if (largestPolyline != null)
            {
                resultPoints = CurveUtils.GetDistinctVertices2D(largestPolyline, null);
                if (resultPoints[0] != resultPoints[resultPoints.Count - 1])
                    resultPoints.Add(resultPoints[0]);
                var clockwise = ComputerGraphics.ClockWise2(resultPoints.ToArray());
                if (clockwise)
                {
                    resultPoints.Reverse();
                }

                using (var transaction = database.TransactionManager.StartTransaction())
                {
                    var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                    var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                    var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                    ObjectId layerId = LayerUtils.AddNewLayer(doc.Database, "temp-poly", "Continuous", color);
                    largestPolyline.Color = color;
                    largestPolyline.LayerId = layerId;
                    modelspace.AppendEntity(largestPolyline);
                    transaction.AddNewlyCreatedDBObject(largestPolyline, add: true);

                    transaction.Commit();
                }
            }

            // 5. Delete the polygons of resultIds
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var newCreatedId in resultIds)
                {
                    if (newCreatedId.IsErased)
                        continue;
                    var dbObj = tr.GetObject(newCreatedId, OpenMode.ForWrite);
                    dbObj.Erase();
                }

                ////////////////////////////////////////////////////////////////////////////////////////
                tr.Commit();
            }

            return resultPoints;
        }
    }
}
