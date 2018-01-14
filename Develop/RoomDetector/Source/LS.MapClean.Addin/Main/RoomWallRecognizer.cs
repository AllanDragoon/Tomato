using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DbxUtils.Utils;

namespace LS.MapClean.Addin.Main
{
    public class RoomWallRecognizer
    {
        public static void Recognize()
        {
            // 1. Get all available entities in model space
            var linearIds = GetLinearEntities();
            // ( We only use polylines and lines, so filter out others.)
            var apartmentContourInfo = GetApartmentContour(linearIds);
            // 2. Get the contour of the apartment.
            // 3. Use the contour to search walls.
            if (apartmentContourInfo.Contour.Count > 0)
            {
                SearchWalls(apartmentContourInfo.Contour, apartmentContourInfo.InternalSegments);
            }
            // 4. Get walls' center lines, and connect them.
            // 5. Find rooms
            // 6. Show the walls and rooms
        }

        private static IEnumerable<ObjectId> GetLinearEntities()
        {
            var result = new List<ObjectId>();
            Database curDb = Application.DocumentManager.MdiActiveDocument.Database;
            using (var transaction = curDb.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(curDb);
                var modelspace = (BlockTableRecord) transaction.GetObject(modelspaceId, OpenMode.ForRead);
                foreach (ObjectId objId in modelspace)
                {
                    var entity = transaction.GetObject(objId, OpenMode.ForRead);
                    if (!IsVisibleLinearEntity(entity, transaction))
                    {
                        continue;
                    }
                    result.Add(objId);
                }
                transaction.Commit();
            }
            return result;
        }

        private static bool IsVisibleLinearEntity(DBObject entity, Transaction transaction)
        {
            var type = entity.GetType();
            if (type != typeof (Polyline) && type != typeof (Line))
                return false;
            var layer = (LayerTableRecord)transaction.GetObject(((Entity)entity).LayerId, OpenMode.ForRead);
            if (layer.IsOff || layer.IsFrozen)
                return false;

            return true;
        }

        private static ApartmentContourInfo GetApartmentContour(IEnumerable<ObjectId> linearIds)
        {
            var info = ApartmentContour.CalcContour(Application.DocumentManager.MdiActiveDocument, linearIds);
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            // Test code !
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var contourSegments = info.Contour;
                var innerSegments = info.InternalSegments;

                // find out windows on wall, usually there are 4 or 3 parallel line segments for a window like below.
                //   -----------------------
                //   -----------------------
                //   -----------------------
                //   -----------------------
                const double maxWallWidth = 600; // todo
                Tolerance tol = new Tolerance(0.001, 0.001);
                List<LineSegment3d> sideWindowSegments = new List<LineSegment3d>();
                List<List<LineSegment3d>> windowGroup = new List<List<LineSegment3d>>();
                for (var i = 0; i < innerSegments.Count; i++)
                {
                    var thisSeg = innerSegments[i];
                    //var startPt = innerSegments[i].StartPoint;
                    //var endPt = innerSegments[i].EndPoint;
                    // do check if the start and end points are on the wall
                    //var cIndex = contourSegments.FindIndex((seg) => {
                    //    if (seg.IsOn(startPt, tol) && seg.IsOn(endPt, tol))
                    //        return true;
                    //    return false;
                    //});

                    // find out if the line segment is on the outer contour wall
                    //var cIndex = contourSegments.FindIndex((seg) =>
                    //{
                    //    if (seg.IsParallelTo(innerSegments[i], tol))
                    //    {
                    //        var dist = seg.GetDistanceTo(thisSeg.MidPoint);
                    //        if (dist < maxWallWidth)
                    //            return true;
                    //    }
                    //    return false;
                    //});
                    //if (cIndex != -1)
                    {
                        // find out all other parallel and equal length segments with this one
                        var startPt = thisSeg.StartPoint;
                        var endPt = thisSeg.EndPoint;
                        double thisLength = thisSeg.Length;
                        Vector3d direction = thisSeg.Direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
                        Line3d startLine = new Line3d(startPt, direction);
                        Line3d endLine = new Line3d(endPt, direction);
                        List<LineSegment3d> parEqualSegs = new List<LineSegment3d>();
                        for (var j = 0; j < innerSegments.Count; j++)
                        {
                            if (i == j) continue; // itself
                            if (thisSeg.IsParallelTo(innerSegments[j]) &&
                                (startLine.IsOn(innerSegments[j].StartPoint, tol) && endLine.IsOn(innerSegments[j].EndPoint, tol)) ||
                                (startLine.IsOn(innerSegments[j].EndPoint, tol) && endLine.IsOn(innerSegments[j].StartPoint, tol)))
                            {
                                parEqualSegs.Add(innerSegments[j]);
                            }
                        }

                        if (parEqualSegs.Count > 1)
                        {
                            Line3d helper1 = new Line3d(thisSeg.MidPoint, thisSeg.Direction);
                            Line3d helper2 = new Line3d(thisSeg.MidPoint, direction);
                            var intersects = helper1.IntersectWith(helper2);
                            var basePt = intersects[0];

                            //direction = (thisSeg.MidPoint - basePt).GetNormal();
                            // sort them by direction
                            parEqualSegs.Add(thisSeg);
                            parEqualSegs.Sort((seg1, seg2) => {
                                Vector3d vector1 = seg1.MidPoint - basePt;
                                Vector3d vector2 = seg2.MidPoint - basePt;
                                if (vector1.DotProduct(direction) > vector2.DotProduct(direction))
                                    return 1;
                                return -1;
                            });

                            List<LineSegment3d> thisWindow = new List<LineSegment3d>();
                            double distance = parEqualSegs[1].MidPoint.DistanceTo(parEqualSegs[0].MidPoint);
                            if (distance < maxWallWidth / 2)
                            {
                                thisWindow.Add(parEqualSegs[0]);
                                thisWindow.Add(parEqualSegs[1]);
                                for (var k = 2; k < parEqualSegs.Count; k++)
                                {
                                    var dist = parEqualSegs[k].MidPoint.DistanceTo(parEqualSegs[k - 1].MidPoint);
                                    if (thisWindow.Count > 3 || Math.Abs(dist - distance) > 100)
                                    {
                                        // we have find out 4 parallel lines or the distance is not equal to previous one
                                        break;
                                    }
                                    thisWindow.Add(parEqualSegs[k]);
                                }

                                if (thisWindow.Count > 2)
                                {
                                    // this will be treaded as a valid window
                                    windowGroup.Add(thisWindow);
                                }
                            }
                        }
                    }
                }

                innerSegments.RemoveAll((seg) => {
                    var index = windowGroup.FindIndex((window) => {
                        return window.Contains(seg);
                    });
                    if (index != -1)
                        return true;
                    return false;
                });
                // end window

                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                var color1 = Color.FromColorIndex(ColorMethod.ByAci, 1); // 1: red
                ObjectId layerId = LayerUtils.AddNewLayer(database, "temp-poly", "Continuous", color1);

                for (var i = 0; i < windowGroup.Count; i++)
                {
                    var window = windowGroup[i];
                    for (var j = 0; j < window.Count; j++)
                    {
                        var start = window[j].StartPoint;
                        var end = window[j].EndPoint;
                        var line = new Line(start, end);
                        line.Color = color1;
                        line.LayerId = layerId;

                        modelspace.AppendEntity(line);
                        transaction.AddNewlyCreatedDBObject(line, add: true);
                    }
                }

                var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                var polyline = new Polyline();
                for (var i = 0; i < contourSegments.Count; i++)
                {
                    var segment = contourSegments[i];
                    var start = segment.StartPoint;
                    polyline.AddVertexAt(i, new Point2d(start.X, start.Y), 0, 0, 0);
                }
                polyline.Closed = true;
                polyline.Color = color;
                polyline.LayerId = layerId;

                modelspace.AppendEntity(polyline);
                transaction.AddNewlyCreatedDBObject(polyline, add: true);

                foreach (var segment in innerSegments)
                {
                    var line = new Line(segment.StartPoint, segment.EndPoint);

                    line.Color = color;
                    line.LayerId = layerId;
                    modelspace.AppendEntity(line);
                    transaction.AddNewlyCreatedDBObject(line, add: true);
                }

                transaction.Commit();
            }
            return info;
        }

        private static void SearchWalls(List<LineSegment3d> outLines, List<LineSegment3d> allLines)
        {
            WallInfor wallInfo = null;
            using (var tolerance = new Utils.SafeToleranceOverride())
            {
                wallInfo = WallRecognizer.getWallinfors(outLines, allLines);
            }
            if (wallInfo == null)
                return;

            // Test Code!
            var visited = new HashSet<WallInfor>();
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                var color = Color.FromColorIndex(ColorMethod.ByAci, 4); // Cyan
                ObjectId layerId = LayerUtils.AddNewLayer(database, "temp-poly2", "Continuous", color);

                var currentInfo = wallInfo;
                var count = 0;
                while (currentInfo != null && currentInfo.outline != null)
                {
                    if (visited.Contains(currentInfo))
                    {
                        break;
                    }
                    var line = new Line(currentInfo.outline.StartPoint, currentInfo.outline.EndPoint);
                    line.Color = color;
                    line.LayerId = layerId;
                    modelspace.AppendEntity(line);
                    transaction.AddNewlyCreatedDBObject(line, add: true);

                    foreach (var innerSeg in currentInfo.innerlines)
                    {
                        var innerLine = new Line(innerSeg.StartPoint, innerSeg.EndPoint);
                        innerLine.Color = color;
                        innerLine.LayerId = layerId;
                        modelspace.AppendEntity(innerLine);
                        transaction.AddNewlyCreatedDBObject(innerLine, add: true);
                    }

                    visited.Add(currentInfo);
                    count++;
                    currentInfo = currentInfo.next;
                    if (currentInfo == wallInfo)
                    {
                        break;
                    }
                }

                transaction.Commit();
            }
        }

        private static IEnumerable<Entity> GetWallCenterLines( /*TBD*/)
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<Entity> GetRooms(IEnumerable<Entity> centerLines)
        {
            throw new NotImplementedException();
        }

        private static void ShowRoomAndWalls( /*TBD*/)
        {
            throw new NotImplementedException();
        }
    }
}
