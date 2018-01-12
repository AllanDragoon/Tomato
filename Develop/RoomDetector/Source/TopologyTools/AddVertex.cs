using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using TopologyTools.Utils;

namespace TopologyTools
{
    public class AddVertex
    {
        [CommandMethod("AVX2")]
        public static void AddVertexToPolyline()
        {
            //过滤选择polyline
            var tvs = new[]
            {
                new TypedValue((int) DxfCode.Operator, "<or"),
                new TypedValue((int) DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int) DxfCode.Start, "POLYLINE"),
                new TypedValue((int) DxfCode.Operator, "or>")
            };
            var filter = new SelectionFilter(tvs);
            var selectionOpts = new PromptSelectionOptions {SingleOnly = true};

            var currDoc = Application.DocumentManager.MdiActiveDocument;
            //选择polyline
            PromptSelectionResult result = currDoc.Editor.GetSelection(selectionOpts, filter);
            if (result.Status != PromptStatus.OK || result.Value.Count < 1)
                return;

            // 需要关闭掉SNAPMODE，消除grid snap的影响，我们只需要到多段线上面
            using (new SnapModeOverrule(false))
            using (new OsModeOverrule(OsModeOverrule.OsModeNEArest))
            {
                var peo = new PromptPointOptions("\n选择线上的点: ") {AllowNone = true};

                // 选中线上点或者cancel命令才退出。
                while (true)
                {
                    var ptResult = currDoc.Editor.GetPoint(peo);
                    if (ptResult.Status == PromptStatus.OK &&
                        IsPointOnCurveGCP(result.Value.GetObjectIds()[0], ptResult.Value))
                    {
                        AddVertexFromPolyline(result.Value.GetObjectIds()[0], ptResult.Value);
                        break;
                    }
                    if (ptResult.Status == PromptStatus.Cancel)
                        break;
                }
            }
        }

        public static void AddVertexFromPolyline(ObjectId curveId, Point3d newPoint)
        {
            var currentDocumnt = Application.DocumentManager.MdiActiveDocument;
            using (Transaction trans = currentDocumnt.TransactionManager.StartTransaction())
            {
                var curve = trans.GetObject(curveId, OpenMode.ForWrite) as Curve;
                AddVertexFromPolyline(trans, curve, newPoint);
                trans.Commit();
            }
        }

        public static void AddVertexFromPolyline(Transaction trans, Curve curve, Point3d newPoint)
        {
            var polyline = curve as Polyline;
            var polyline2d = curve as Polyline2d;

            // 只支持Polyline和Polyline2d
            if(polyline == null && polyline2d == null)
                return;

            Point3d pointOnCurve = pickPointOnPline(curve, newPoint);
            double param = curve.GetParameterAtPoint(pointOnCurve);
            var index = (int)param;
            if (polyline != null)
            {
                int endParam = polyline.Closed == true ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
                double bulge = polyline.GetBulgeAt(index);
                polyline.AddVertexAt(index + 1, new Point2d(newPoint.X, newPoint.Y), bulge, 0, 0);
            }
            else if (polyline2d != null)
            {
                int i = 0;
                Vertex2d perviousVertex = null;
                foreach (ObjectId vertexId in polyline2d)
                {
                    if (i == index)
                    {
                        var v2d = trans.GetObject(vertexId, OpenMode.ForWrite) as Vertex2d;
                        if (v2d == null)
                            continue;
                        perviousVertex = v2d;
                        break;
                    }
                    i++;
                }
                if (perviousVertex != null)
                {
                    var newVertex = new Vertex2d(newPoint, perviousVertex.Bulge, perviousVertex.StartWidth,
                        perviousVertex.EndWidth, perviousVertex.Tangent);

                    polyline2d.InsertVertexAt(perviousVertex.ObjectId, newVertex);
                    trans.AddNewlyCreatedDBObject(newVertex, true);
                }
            }
        }

        private static Point3d pickPointOnPline(Curve curve, Point3d pt)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            pt = pt.TransformBy(ed.CurrentUserCoordinateSystem);
            Vector3d vdir = ed.GetCurrentView().ViewDirection;
            pt = pt.Project(curve.GetPlane(), vdir);
            return curve.GetClosestPointTo(pt, false);
        }

        // WL：可能后面需要将一些common的util提出来

        /// <summary>
        /// http://through-the-interface.typepad.com/through_the_interface/2012/01/testing-whether-a-point-is-on-any-autocad-curve-using-net.html
        /// A generalised IsPointOnCurve function that works on all
        /// types of Curve (including Polylines), and checks the position
        /// of the returned point rather than relying on catching an exception
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static bool IsPointOnCurveGCP(ObjectId curveId, Point3d pt)
        {
            using (Transaction trans = Application.DocumentManager.MdiActiveDocument.TransactionManager.StartTransaction())
            {
                try
                {
                    var cv = trans.GetObject(curveId, OpenMode.ForRead) as Curve;
                    if (cv != null)
                    {
                        // Return true if operation succeeds
                        Point3d p = cv.GetClosestPointTo(pt, false);
                        return (p - pt).Length <= Tolerance.Global.EqualPoint;
                    }
                }
                catch
                {
                }

                trans.Abort();
            }

            // Otherwise we return false
            return false;
        }
    }
}
