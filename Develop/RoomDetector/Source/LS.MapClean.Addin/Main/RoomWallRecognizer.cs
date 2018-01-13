using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

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

        private static IEnumerable<Entity> GetApartmentContour(IEnumerable<ObjectId> linearIds)
        {
            var point2ds = ApartmentContour.CalcContour(Application.DocumentManager.MdiActiveDocument, linearIds);
            var result = new List<Line>();
            for (var i = 0; i < point2ds.Count - 1; i++)
            {
                var startPt2 = point2ds[i];
                var endPt2 = point2ds[i + 1];
                var startPt = new Point3d(startPt2.X, startPt2.Y, 0);
                var endPt = new Point3d(endPt2.X, endPt2.Y, 0);
                var line = new Line(startPt, endPt);
                result.Add(line);
            }
            return result;
        }

        private static /*TBD*/void SearchWalls(IEnumerable<Entity> entities)
        {
            throw new NotImplementedException();
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
