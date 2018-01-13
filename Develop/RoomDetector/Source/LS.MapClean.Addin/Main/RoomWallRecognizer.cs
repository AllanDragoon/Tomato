using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace LS.MapClean.Addin.Main
{
    public class RoomWallRecognizer
    {
        public static void Recognize()
        {
            // 1. Get all available entities in model space
            // ( We only use polylines and lines, so filter out others.)
            // 2. Get the contour of the apartment.
            // 3. Use the contour to search walls.
            // 4. Get walls' center lines, and connect them.
            // 5. Find rooms
            // 6. Show the walls and rooms
        }

        private static IEnumerable<ObjectId> GetLinearEntities()
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<Entity> GetApartmentContour()
        {
            throw new NotImplementedException();
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
