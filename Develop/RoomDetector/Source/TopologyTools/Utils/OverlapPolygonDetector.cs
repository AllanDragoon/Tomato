using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Index.Quadtree;
using NetTopologySuite.Operation.Polygonize;
using TopologyTools.ReaderWriter;

namespace TopologyTools.Utils
{
    public class TopologyData
    {
        public Quadtree<IGeometry> Quadtree { get; set; }
        public List<IGeometry> Geometries { get; set; }
        public Dictionary<ObjectId, IGeometry> GeometryDictionary { get; set; }
        public HashSet<ObjectId> WrongEnvelopeObjects { get; set; }
        public HashSet<ObjectId> InvalidObjects { get; set; }

        public TopologyData()
        {
            GeometryDictionary = new Dictionary<ObjectId, IGeometry>();
            Quadtree = new Quadtree<IGeometry>();
            Geometries = new List<IGeometry>();
            WrongEnvelopeObjects = new HashSet<ObjectId>();
            InvalidObjects = new HashSet<ObjectId>();
        }
    }

    public class GeometryOverlap
    {
        public ObjectId ThisGeometry { get; set; }
        public ObjectId ThatGeometry { get; set; }
        public Region IntersectRegion { get; set; }

        public GeometryOverlap()
        {
        }
    }

    public class PolygonOverlaps
    {
        public List<GeometryOverlap> GeometryOverlaps { get; set; }
        public HashSet<ObjectId> CannotCreateRegions { get; set; }
        public List<KeyValuePair<ObjectId, ObjectId>> CannotBooleanRegions { get; set; }
        public PolygonOverlaps()
        {
            GeometryOverlaps = new List<GeometryOverlap>();
            CannotCreateRegions = new HashSet<ObjectId>();
            CannotBooleanRegions = new List<KeyValuePair<ObjectId, ObjectId>>();
        }
    }

    public class OverlapPolygonDetector
    {
        public static void FindOverlapingPolylines()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var polylines = CadUtils.FindAllPolylines(document);
            FindOverlapingPolylines(polylines);
        }

        public static void FindOverlapingPolylines(ObjectId[] objectIds)
        {
            // 1. 建立拓扑关系
            var topoData = BuildTopology(objectIds);

            // 2. 清理掉所有的临时图形
            PolylineTransientGraphics.ClearTransientGraphics();

            var findOverlap = new FindOverlap();

            // 3. 查找重叠的几何图元
            var polygonOverlaps = FindOverlappingGeometries(findOverlap, topoData, objectIds);

            // 4. 画出所有几何图元
            DrawOverlappingGeometries(topoData, polygonOverlaps);

            findOverlap.ReleaseRegions();

            // 5. 输出到窗口
            var document = Application.DocumentManager.MdiActiveDocument;
            document.Editor.WriteMessage(String.Format("一共有{0}处重叠", polygonOverlaps.GeometryOverlaps.Count));

            OutputMessage(polygonOverlaps.CannotCreateRegions, "不能造区");
            OutputMessage(polygonOverlaps.CannotBooleanRegions, "不能作布尔运算");
            OutputMessage(topoData.WrongEnvelopeObjects, "包围盒计算错误，可能有重复点");
            OutputMessage(topoData.InvalidObjects, "内部拓扑错误");
        }

        public static PolygonOverlaps FindPolygonOverlaps(ObjectId[] objectIds)
        {
            // 1. 建立拓扑关系
            var topoData = BuildTopology(objectIds);
            var findOverlap = new FindOverlap();

            // 3. 查找重叠的几何图元
            var polygonOverlaps = FindOverlappingGeometries(findOverlap, topoData, objectIds);
            return polygonOverlaps;
        }

        public static void OutputMessage(HashSet<ObjectId> objectIds, string wrongType)
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (objectIds.Any())
            {
                string message = String.Format("一共有{0}处{1}，句柄是：", objectIds.Count, wrongType);
                foreach (var objectId in objectIds)
                {
                    message += objectId.Handle + ",";
                }
                document.Editor.WriteMessage("\n");
                document.Editor.WriteMessage(message);
            }
        }

        public static void OutputMessage(List<KeyValuePair<ObjectId, ObjectId>> objectIds, string wrongType)
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (objectIds.Any())
            {
                string message = String.Format("一共有{0}处{1}，句柄是：", objectIds.Count, wrongType);
                foreach (var objectId in objectIds)
                {
                    message += objectId.Key.Handle + "," + objectId.Value.Handle + ";";
                }
                document.Editor.WriteMessage("\n");
                document.Editor.WriteMessage(message);
            }
        }

        private static TopologyData BuildTopology(ObjectId[] objectIds)
        {
            var topoData = new TopologyData();
            var database = objectIds[0].Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                foreach (ObjectId objectId in objectIds)
                {
                    // 如果没有
                    var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve == null || !curve.Visible)
                        continue;

                    // 图层关闭，继续，因为有可能是图幅层
                    var layer = (LayerTableRecord)tr.GetObject(curve.LayerId, OpenMode.ForRead);
                    if (layer.IsOff)
                        continue;

                    // 目前只处理封闭的和大于3的多边形
                    // http://192.168.0.6:8080/browse/LCLIBCAD-903
                    // 否则会弹出points must form a closed linestring异常
                    if (curve.Closed 
                        && reader.NumberOfVerticesMoreThan3(curve))
                    {
                        var geom = reader.ReadCurveAsPolygon(tr, curve) as IGeometry;
                        geom = geom.Buffer(0);
                        if (!geom.IsValid)
                        {
                            topoData.InvalidObjects.Add(objectId);
                            System.Diagnostics.Trace.WriteLine(geom.ToString());
                        }
                        // 没有包围盒，无法进入空间索引 
                        else if (geom.EnvelopeInternal.IsNull)
                        {
                            topoData.WrongEnvelopeObjects.Add(objectId);
                        }
                        else
                        {
                            //geom = SimpleGeometryPrecisionReducer.Reduce(geom, pmFixed3);
                            geom.UserData = objectId;
                            topoData.Quadtree.Insert(geom.EnvelopeInternal, geom);
                            topoData.Geometries.Add(geom);
                            topoData.GeometryDictionary[objectId] = geom;
                        }
                    }
                }

                tr.Commit();
            }

            return topoData;
        }

        private static PolygonOverlaps FindOverlappingGeometries(FindOverlap findOverlap, TopologyData topoData, ObjectId[] objectIds)
        {
            var overlap = new PolygonOverlaps();
            var handledPairs = new HashSet<string>();
            var count = 0;
            foreach (var geom in topoData.Geometries)
            {
                // 先用NTS建立拓扑，初查，哪些是重叠的，非常快速。
                var nearGeoms = GetNearGeometries(geom, topoData.Quadtree);
                var thisObjId = (ObjectId)geom.UserData;

                var ids = new ObjectIdCollection();
                foreach (var geometry in nearGeoms)
                {
                    var nearObjId = (ObjectId)geometry.UserData;
                    if (nearObjId != thisObjId)
                    {
                        // 记录当前处理的object对，这样避免重复计算一对相邻的多边形
                        var handle = thisObjId + "_" + nearObjId;
                        var handle2 = nearObjId + "_" + thisObjId;

                        //var thisGeometry = topoData.GeometryDictionary[thisObjId];
                        if (!handledPairs.Contains(handle) && !handledPairs.Contains(handle2))
                        {
                            // 先记录下来，处理过的，后面就不再处理了
                            handledPairs.Add(handle);
                           // var nearGeometry = topoData.GeometryDictionary[nearObjId];
                            try 
                            {
                                //var intersect = thisGeometry.Overlaps(nearGeometry);
                                //if (intersect)
                                {
                                    count++;
                                    // 在NTS里面也许是接边的也算重叠的
                                    // 再通过ACAD面域的计算，相交，如果有交集，则认为是有重叠的，而且画出交集。
                                    var regionIntersection = CadUtils.GetIntersectionPart(thisObjId, nearObjId);
                                    
                                    //var region = findOverlap.GetIntersectionPart(thisObjId, nearObjId);
                                    if (regionIntersection != null)
                                    {
                                        // 相交了，有相交的Region
                                        if (regionIntersection.ObjectId1.IsNull)
                                        {
                                            ids.Add(nearObjId);
                                            overlap.GeometryOverlaps.Add(new GeometryOverlap()
                                            {
                                                ThisGeometry = thisObjId,
                                                ThatGeometry = nearObjId,
                                                IntersectRegion = regionIntersection.Region
                                            });
                                        }
                                        // 没有相交，可能是造区错误
                                        else if (regionIntersection.ObjectId1.IsNull)
                                        {
                                            overlap.CannotCreateRegions.Add(regionIntersection.ObjectId1);
                                        }
                                        // Object1/Object2
                                        // 造区直接boolean运算发生错误
                                        // 面域上的布尔运算失败 不同体顶点的重合 face_face_ints
                                        else if (!regionIntersection.ObjectId1.IsNull && !regionIntersection.ObjectId2.IsNull)
                                        {
                                            overlap.CannotBooleanRegions.Add(
                                                new KeyValuePair<ObjectId, ObjectId>(regionIntersection.ObjectId1, 
                                                    regionIntersection.ObjectId2));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Trace.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine(count);
            return overlap;
        }

        public static void DrawOverlappingGeometries(TopologyData topoData, PolygonOverlaps overlaps)
        {
            if (!overlaps.GeometryOverlaps.Any())
                return;

            var regions = new List<Region>();
            var database = overlaps.GeometryOverlaps[0].ThisGeometry.Database;

            // 读取CAD图元，并且绘制出来
            foreach (var overlap in overlaps.GeometryOverlaps)
            {
                regions.Add(overlap.IntersectRegion);
            }

            // 画出来
            PolylineTransientGraphics.CreateTransientRegions(database, regions);
        }

        public static List<IGeometry> GetNearGeometries(IGeometry me, Quadtree<IGeometry> others, double buffer)
        {
            var bufferedGeom = me.Buffer(0);
            var preparedGeometry = PreparedGeometryFactory.Prepare(bufferedGeom);
            var geometries = new List<IGeometry>();
            foreach (var geom in others.Query(bufferedGeom.EnvelopeInternal))
            {
                var poly = geom as IPolygon;
                if (poly != null && preparedGeometry.Overlaps(poly.ExteriorRing))
                    geometries.Add(poly);
            }
            return geometries;
        }

        public static List<IGeometry> GetNearGeometries(IGeometry geometry, Quadtree<IGeometry> others)
        {
            var geometries = new List<IGeometry>();
            foreach (var geom in others.Query(geometry.EnvelopeInternal))
            {
                var polyline = geom as IPolygon;
                if (polyline != null && geometry.Intersects(polyline))
                    geometries.Add(polyline);
            }
            return geometries;
        }

        internal static IGeometry Polygonize(IGeometry geometry)
        {
            var lines = LineStringExtracter.GetLines(geometry);
            var polygonizer = new Polygonizer();
            polygonizer.Add(lines);
            var polys = polygonizer.GetPolygons();
            var polyArray = GeometryFactory.ToGeometryArray(polys);
            return geometry.Factory.CreateGeometryCollection(polyArray);
        }

        internal static IGeometry SplitPolygon(IGeometry polygon, IGeometry line)
        {
            var nodedLinework = polygon.Boundary.Union(line);
            var polygons = Polygonize(nodedLinework);

            // only keep polygons which are inside the input
            var output = new List<IGeometry>();
            for (var i = 0; i < polygons.NumGeometries; i++)
            {
                var candpoly = (Polygon)polygons.GetGeometryN(i);
                if (polygon.Contains(candpoly))
                    output.Add(candpoly);
            }

            /* 
            return polygon.Factory.CreateGeometryCollection( 
                GeometryFactory.ToGeometryArray(output)); 
             */

            return polygon.Factory.BuildGeometry(output);
        }
    }
}
