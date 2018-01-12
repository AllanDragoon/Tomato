using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Utils
{
    public static class EditorUtils
    {
        /// <summary>
        /// Zoom using a view object
        /// </summary>
        /// <param name="ed"></param>
        /// <param name="ext"></param>
        /// <param name="factor"></param>
        public static void ZoomToWin(this Editor ed, Extents3d ext, double factor = 1.0)
        {
            var min2D = new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
            var max2D = new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y);
            using (var view = new ViewTableRecord())
            {
                view.CenterPoint = min2D + ((max2D - min2D) / 2.0);
                view.Height = (max2D.Y - min2D.Y) * factor;
                view.Width = (max2D.X - min2D.X) * factor;
                ed.SetCurrentView(view);
            }
        }

        /// <summary>
        /// http://through-the-interface.typepad.com/through_the_interface/2006/11/two_methods_for.html
        /// Selecting entities at a particular location
        /// </summary>
        /// <param name="ed"></param>
        /// <param name="position"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static ObjectId[] SelectEntitiesAtPoint(this Editor ed, SelectionFilter filter, Point3d position, double tolerance = 0.1)
        {
            Point3d minPt = new Point3d(position.X - tolerance, position.Y - tolerance, 0.0);
            Point3d maxPt = new Point3d(position.X + tolerance, position.Y + tolerance, 0.0);
            var extents = new Extents3d(minPt, maxPt);
            ed.ZoomToWin(extents, factor:2.0);
            var selectionResult = ed.SelectCrossingWindow(minPt, maxPt, filter);
            if (selectionResult.Value == null)
                return new ObjectId[0];
            return selectionResult.Value.GetObjectIds();
        }

        public static List<ObjectId> SelectObjectsByEntityBoundingBox(Editor editor, SelectionFilter filter, Entity entity, double extraBuffer)
        {
            var result = new List<ObjectId>();
            var database = editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var extent = GeometryUtils.SafeGetGeometricExtents(entity);
                if (extent != null)
                {
                    var boundaryInModelSpace = new Point3dCollection
                    {
                        new Point3d(extent.Value.MinPoint.X - extraBuffer, extent.Value.MinPoint.Y - extraBuffer, 0),
                        new Point3d(extent.Value.MinPoint.X - extraBuffer, extent.Value.MaxPoint.Y + extraBuffer, 0),
                        new Point3d(extent.Value.MaxPoint.X + extraBuffer, extent.Value.MaxPoint.Y + extraBuffer, 0),
                        new Point3d(extent.Value.MaxPoint.X + extraBuffer, extent.Value.MinPoint.Y - extraBuffer, 0)
                    };

                    var extentWithBuffer = new Extents3d(boundaryInModelSpace[0], boundaryInModelSpace[2]);

                    // SelectCrossingPolygon要求图一定要在视口内部，要不然会查不到，或者漏掉。
                    // 需要设定一下viewPort后abort.
                    ZoomToWin(editor, extentWithBuffer, 2.0);
                    var res = editor.SelectCrossingPolygon(boundaryInModelSpace, filter);
                    if (res != null && res.Status == PromptStatus.OK)
                    {
                        // Aboort viewport change
                        result.AddRange(res.Value.GetObjectIds());
                    }
                    transaction.Abort();
                }
            }
            return result;
        }
    }
}
