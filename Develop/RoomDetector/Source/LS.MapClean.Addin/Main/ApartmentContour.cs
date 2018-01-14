using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal class ApartmentContourInfo
    {
        public List<LineSegment3d> Contour { get; set; }
        public List<LineSegment3d> InternalSegments { get; set; }
    }

    internal class ApartmentContour
    {
        public static ApartmentContourInfo CalcContour(Document doc, IEnumerable<ObjectId> objectIdsList)
        {
            var newCreatedIds = new List<ObjectId>();
            IEnumerable<ObjectId> curveIds = new List<ObjectId>();
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

                curveIds = checkIds;

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
            doc.Editor.WriteMessage("开始分析外墙轮廓...");
            var resultIds = new List<ObjectId>();
            if (curveIds.Any())
            {
                var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                ObjectId layerId = LayerUtils.AddNewLayer(doc.Database, "temp-poly", "Continuous", color);

                using (var tolerance = new SafeToleranceOverride())
                using (var waitCursor = new WaitCursorSwitcher())
                {
                    //var polygons = MinimalLoopSearcher2.Search(curveIds, doc);
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
                    resultIds = NtsUtils.PolygonizeLineStrings(doc.Database, curveIds, "temp-poly", color, 0.0001);
                }
            }
            
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
            if (polylines.Count >= 2)
            {
                var first = polylines[0];
                var second = polylines[1];
                // Exclude the situation if the largest polyline is a drawing frame.
                if (IsRectangle(first) && HaveSomeTextsOnBottom(first, database) &&
                    PolygonIncludeSearcher.IsInclude(first, second, null))
                {
                    polylines.RemoveAt(0);
                }
            }
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

                //// Test code !
                //using (var transaction = database.TransactionManager.StartTransaction())
                //{
                //    var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                //    var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                //    var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                //    ObjectId layerId = LayerUtils.AddNewLayer(doc.Database, "temp-poly", "Continuous", color);
                //    largestPolyline.Color = color;
                //    largestPolyline.LayerId = layerId;
                //    modelspace.AppendEntity(largestPolyline);
                //    transaction.AddNewlyCreatedDBObject(largestPolyline, add: true);


                //    foreach (var polyline in polylines)
                //    {
                //        if (polyline == largestPolyline)
                //            continue;

                //        polyline.Color = color;
                //        polyline.LayerId = layerId;
                //        modelspace.AppendEntity(polyline);
                //        transaction.AddNewlyCreatedDBObject(polyline, add: true);
                //    }

                //    transaction.Commit();
                //}
            }

            // Get contour linesegments from resultPoints
            var contourSegments = new List<LineSegment3d>();
            var innerSegments = new List<LineSegment3d>();
            if (resultPoints.Count > 0)
            {
                for (var i = 0; i < resultPoints.Count - 1; i++)
                {
                    var start = new Point3d(resultPoints[i].X, resultPoints[i].Y, 0);
                    var end = new Point3d(resultPoints[i + 1].X, resultPoints[i + 1].Y, 0);
                    var segment = new LineSegment3d(start, end);
                    contourSegments.Add(segment);
                }
                // Get inner linesegments
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var contourArray = resultPoints.ToArray();
                    foreach (var objId in curveIds)
                    {
                        var point2ds = CurveUtils.GetDistinctVertices2D(objId, tr);
                        
                        for (var i = 0; i < point2ds.Count - 1; i++)
                        {
                            var start = point2ds[i];
                            var end = point2ds[i + 1];
                            if (start.IsEqualTo(end))
                                continue;

                            // TODO: no need to calculate again for startInPoly.
                            var startInPoly = ComputerGraphics.IsInPolygon2(start, contourArray);
                            var endInPoly = ComputerGraphics.IsInPolygon2(end, contourArray);
                            
                            if ((startInPoly == 0 && endInPoly == 0) ||
                                (startInPoly == -1 && endInPoly == -1))
                                continue;
                            var segment = new LineSegment3d(new Point3d(start.X, start.Y, 0),
                                new Point3d(end.X, end.Y, 0));
                            innerSegments.Add(segment);
                        }
                    }
                    tr.Commit();
                }
            }

            // 5. Delete the polygons of resultIds
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var objId in resultIds)
                {
                    if (objId.IsErased)
                        continue;
                    var dbObj = tr.GetObject(objId, OpenMode.ForWrite);
                    dbObj.Erase();
                }

                ////////////////////////////////////////////////////////////////////////////////////////
                tr.Commit();
            }

            // Delete the splited curves
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

            var result = new ApartmentContourInfo()
            {
                Contour = contourSegments,
                InternalSegments = innerSegments
            };
            return result;
        }

        private static bool IsRectangle(Polyline polyline)
        {
            var extents = polyline.GeometricExtents;
            var width = extents.MaxPoint.X - extents.MinPoint.X;
            var height = extents.MaxPoint.Y - extents.MinPoint.Y;
            var idealArea = Math.Abs(width*height);
            if (idealArea.EqualsWithTolerance(0.0))
                return false;
            var area = Math.Abs(polyline.Area);
            var ratio = area/idealArea;
            return ratio.LargerOrEqual(0.95);
        }

        private static bool HaveSomeTextsOnBottom(Polyline polyline, Database database)
        {
            var extent = polyline.GeometricExtents;

            var path = ExtentsToPoint2ds(extent);
            var textCount = 0;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForRead);
                
                foreach (ObjectId objId in modelspace)
                {
                    if (textCount >= 5)
                        break;

                    var entity = transaction.GetObject(objId, OpenMode.ForRead);
                    var text = entity as DBText;
                    var mtext = entity as MText;
                    if (text == null && mtext == null)
                    {
                        continue;
                    }

                    if (!IsVisibleEntity(entity, transaction))
                        continue;

                    Point3d textPosition;
                    if (text != null)
                    {
                        textPosition = text.Position;
                    }
                    else
                    {
                        textPosition = mtext.Location;
                    }
                    var pos2d = new Point2d(textPosition.X, textPosition.Y);
                    if (ComputerGraphics.IsInPolygon2(pos2d, path) == 1)
                    {
                        textCount++;
                    }
                }
                transaction.Commit();
            }
            return textCount >= 5;
        }

        private static Point2d[] ExtentsToPoint2ds(Extents3d extent)
        {
            var height = extent.MaxPoint.Y - extent.MinPoint.Y;
            var pt1 = new Point2d(extent.MinPoint.X, extent.MinPoint.Y);
            var pt2 = new Point2d(extent.MaxPoint.X, extent.MinPoint.Y);
            var pt3 = new Point2d(extent.MaxPoint.X, extent.MinPoint.Y + height / 5);
            var pt4 = new Point2d(extent.MinPoint.X, extent.MinPoint.Y + height / 5);
            var path = new Point2d[] { pt1, pt2, pt3, pt4 };
            return path;
        }

        private static bool IsVisibleEntity(DBObject entity, Transaction transaction)
        {
            var layer = (LayerTableRecord)transaction.GetObject(((Entity)entity).LayerId, OpenMode.ForRead);
            if (layer.IsOff || layer.IsFrozen)
                return false;

            return true;
        }
    }
}
