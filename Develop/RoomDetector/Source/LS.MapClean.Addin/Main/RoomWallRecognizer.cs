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
            GetApartmentContour(linearIds);
            // 2. Get the contour of the apartment.
            // 3. Use the contour to search walls.
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

                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                var color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
                ObjectId layerId = LayerUtils.AddNewLayer(database, "temp-poly", "Continuous", color);
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

        private static void SearchWalls(IEnumerable<Line> entities)
        {
            foreach (Line line in entities)
            {
                LineSegment3d lineSeg2 = WallRecognizer.getWallline(line, entities);  // Daniel: entities should be instead
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
