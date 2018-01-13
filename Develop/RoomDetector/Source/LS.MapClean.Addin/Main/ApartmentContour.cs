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
        public static IEnumerable<DBPoint> CalcContour(Document doc, IEnumerable<ObjectId> linearIds)
        {
            using (var waitCursor = new WaitCursorSwitcher())
            using (var tolerance = new SafeToleranceOverride())
            {
                // 1. Break down all lines
                doc.Editor.WriteMessage("\n打断所有交叉线...\n");
                var breakCrossingAlgorithm = new BreakCrossingObjectsQuadTree(doc.Editor);
                // 2. Make polygons.
                // 3. Union all polygons
                // 4. Get largest are polygon which is the apartment's contour
            }
        }
    }
}
