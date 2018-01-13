using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using GeoAPI.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TopologyTools.ReaderWriter;
using TopologyTools.Utils;

namespace TopologyTools
{
    public class Commands
    {
        [CommandMethod("CB1")]
        public static void CreateBuffer()
        {
            //过滤选择polyline
            var selected = GetSelectPolyline(true);
            if (selected.Count > 0)
                NtsUtils.CreateBuffer(selected[0], 5);
        }

        [CommandMethod("SI1")]
        public static void SelfIntersection()
        {
            //过滤选择polyline
            var selected = GetSelectPolyline(true);
            if (selected.Count > 0)
                NtsUtils.LineStringSelfIntersections(selected[0]);
        }

        [CommandMethod("IP")]
        public static void SelfIntersection2()
        {
            //过滤选择polyline
            //var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var selected = GetSelectPolyline(true);
            if (selected.Count == 2)
            {
                PolylineNoder.DrawNodes(selected[0], selected[1]);
            }
            //NtsUtils.FindTouchedEdge(new ObjectId[] { selected[0], selected[1]});
        }

        [CommandMethod("TE")]
        public static void FindTouchedEdge()
        {
            //过滤选择polyline
            //var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var selected = GetSelectPolyline(true);
            if (selected.Count == 2)
            {
                NtsUtils.FindTouchedEdge(new ObjectId[] { selected[0], selected[1]});
            }
        }

        [CommandMethod("NG")]
        public static void GetNearGeometries()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var pts = PolylineNoder.GetGeometryNodes(document, 0.5);
            PolylineNoder.DrawPoints(document.Database, pts);
        }

        [CommandMethod("PN1")]
        public static void Polygonizer()
        {
            //过滤选择polyline
            //var selected = GetSelectPolyline(false);
            //if (selected.Count > 0)
            //    NtsUtils.PolygonizeLineStrings(selected[0].Database, selected);
        }

        [CommandMethod("XGX")]
        public static void FindDanglingLine()
        {
            var watch = Stopwatch.StartNew();

            var document = Application.DocumentManager.MdiActiveDocument;
            NtsUtils.FindDanglingLine(document.Database);

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            document.Editor.WriteMessage("\n花费时间{0}毫秒", elapsedMs);
        }

        [CommandMethod("CO")]
        public static void CheckOverlapping()
        {
            OverlapPolygonDetector.FindOverlapingPolylines();
        }

        public static Dictionary<ObjectId, IList<Point3d>> FindDanglingLine(IList<ObjectId> objectIds)
        {
            var dictionary = new Dictionary<ObjectId, IList<Point3d>>();
            //var points = new List<Point3d>();
            var database = objectIds[0].Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                // var pmFixed3 = new PrecisionModel(3);
                // 读入多边形数据
                foreach (ObjectId objectId in objectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    IGeometry geom = reader.ReadEntityAsGeometry(tr, objectId);
                    if (geom == null)
                        continue;

                    // 开始做Union
                    var nodedLineString = UnaryUnionOp.Union(geom);
                    var polygonizer = new Polygonizer();
                    polygonizer.Add(nodedLineString);
                    var polygons = polygonizer.GetPolygons();

                    // 悬挂线
                    var points = new List<Point3d>();
                    foreach (ILineString lineString in polygons)
                    {
                        foreach (var coordinate in lineString.Coordinates)
                        {
                            // 如果是NaN直接设定为0
                            if (double.IsNaN(coordinate.Z))
                                coordinate.Z = 0;

                            points.Add(new Point3d(coordinate.X, coordinate.Y, coordinate.Z));
                        }
                    }
                    if (points.Any())
                        dictionary.Add(objectId, points);
                }
                tr.Commit();
            }

            return dictionary;
        }

        static ObjectIdCollection GetSelectPolyline(bool isSingleSelect)
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
            var selectionOpts = new PromptSelectionOptions { SingleOnly = isSingleSelect };

            var currDoc = Application.DocumentManager.MdiActiveDocument;
            //选择polyline
            PromptSelectionResult result = currDoc.Editor.GetSelection(selectionOpts, filter);
            if (result.Status == PromptStatus.OK && result.Value.Count >= 1)
            {
                return new ObjectIdCollection(result.Value.GetObjectIds());
            }

            return new ObjectIdCollection();
        }
    }
}