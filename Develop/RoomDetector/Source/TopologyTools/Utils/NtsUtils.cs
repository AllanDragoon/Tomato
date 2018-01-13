#define TryManualUnion

using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DbxUtils.Utils;
using GeoAPI.Geometries;
using GeoAPI.Operation.Buffer;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Index.Quadtree;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Precision;
using TopologyTools.ReaderWriter;
using System.Threading;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace TopologyTools.Utils
{
    public class NtsUtils
    {
        public static void CreateBuffer(ObjectId polylineId, double bufferDist)
        {
            var database = polylineId.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;

                // 读取CAD图元
                var reader = new DwgReader();
                var lineString = reader.ReadGeometry(curve, tr) as LineString;

                // 做出缓冲区
                var buffer = lineString.Buffer(bufferDist) as Geometry;
                if (buffer.GeometryType == "Polygon")
                {
                    var writer = new DwgWriter();
                    Polyline[] polylines = writer.WritePolyline(buffer as IPolygon);

                    // 输出到CAD
                    foreach (var polyline in polylines)
                        CadUtils.AddToCurrentDb(tr, database, polyline);
                }

                tr.Commit();
            }
        }

        public static void Touch(ObjectId[] polylineIds)
        {
            var database = polylineIds[0].Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var polylineId in polylineIds)
                {
                    var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;

                    // 读取CAD图元
                    var reader = new DwgReader();
                    var lineString = reader.ReadGeometry(curve, tr) as LineString;
                    //lineString.Touches()

                    // 做出缓冲区
                    var geometry = LineStringSelfIntersectionsOp(lineString);

                    if (geometry.GeometryType == "Point")
                    {
                        var writer = new DwgWriter();
                        DBPoint dbPt = writer.WriteDbPoint(geometry as Point);
                        CadUtils.DrawPoint(tr, database, dbPt);
                    }
                }

                tr.Commit();
            }
        }

        public static void LineStringSelfIntersections(ObjectId polylineId)
        {
            var database = polylineId.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;

                // 读取CAD图元
                var reader = new DwgReader();
                var lineString = reader.ReadGeometry(curve, tr) as LineString;

                // 做出缓冲区
                var geometry = LineStringSelfIntersectionsOp(lineString);
                if (geometry == null || geometry.IsEmpty)
                    return;

                var writer = new DwgWriter();
                var modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var blockTableRecord = (BlockTableRecord) tr.GetObject(modelSpaceId, OpenMode.ForWrite, false);
                if (geometry.GeometryType == "Point")
                {
                    DBPoint dbPt = writer.WriteDbPoint(geometry as Point);

                    var mode = (short) Application.GetSystemVariable("pdmode");
                    if (mode == 0)
                        Application.SetSystemVariable("pdmode", 99);
                    dbPt.ColorIndex = 1;

                    // 输出到CAD
                    blockTableRecord.AppendEntity(dbPt);
                    tr.AddNewlyCreatedDBObject(dbPt, true);
                }
                else if (geometry.GeometryType == "MultiPoint")
                {
                    var multipPoint = geometry as MultiPoint;
                    if (multipPoint == null)
                        return;
                    var dbPoints = writer.WriteDbPoints(multipPoint);
                    foreach (DBPoint dbPt in dbPoints)
                    {
                        var mode = (short) Application.GetSystemVariable("pdmode");
                        if (mode == 0)
                            Application.SetSystemVariable("pdmode", 99);
                        dbPt.ColorIndex = 1;

                        // 输出到CAD
                        blockTableRecord.AppendEntity(dbPt);
                        tr.AddNewlyCreatedDBObject(dbPt, true);
                    }
                }
                tr.Commit();
            }
        }

        public static IGeometry LineStringSelfIntersectionsOp(ILineString line)
        {
            IGeometry lineEndPts = GetEndPoints(line);
            IGeometry nodedLine = line.Union(lineEndPts);
            IGeometry nodedEndPts = GetEndPoints(nodedLine);
            IGeometry selfIntersections = nodedEndPts.Difference(lineEndPts);
            return selfIntersections;
        }

        /// <summary> 
        /// Gets endpoints of LineString and MultiLineString Geometries 
        /// Original code from GisSharpBlog.NetTopologySuite.Samples.Technique.LineStringSelfIntersections.cs 
        /// </summary> 
        /// <param name="g">Linear geometry</param> 
        /// <returns>Multipoint geometry</returns> 
        private static IGeometry GetEndPoints(IGeometry g)
        {
            var endPtList = new List<Coordinate>();
            if (g is ILineString)
            {
                var line = (ILineString) g;
                endPtList.Add(line.GetCoordinateN(0));
                endPtList.Add(line.GetCoordinateN(line.NumPoints - 1));
            }
            else if (g is IMultiLineString)
            {
                var mls = (IMultiLineString) g;
                for (int i = 0; i < mls.NumGeometries; i++)
                {
                    var line = (ILineString) mls.GetGeometryN(i);
                    endPtList.Add(line.GetCoordinateN(0));
                    endPtList.Add(line.GetCoordinateN(line.NumPoints - 1));
                }
            }
            var endPts = endPtList.ToArray();
            return GeometryFactory.Default.CreateMultiPoint(endPts);
        }

        public static Dictionary<ObjectId, IList<ObjectId>> GetNearGeometries(IList<ObjectId> objectIds, double buffer, bool forceClosed = true)
        {
            var dictionary = new Dictionary<ObjectId, IList<ObjectId>>();

            var database = objectIds[0].Database;
            var quadtree = new Quadtree<IGeometry>();
            var geometries = new List<IGeometry>();
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                foreach (ObjectId objectId in objectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    Debug.WriteLine("{0}", objectId.Handle);

                    var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    // 目前只处理封闭的和大于3的多边形
                    // http://192.168.0.6:8080/browse/LCLIBCAD-903
                    // 否则会弹出“points must form a closed linestring”异常
                    if (forceClosed && !curve.Closed || !reader.NumberOfVerticesMoreThan3(curve))
                        continue;

                    var geom = reader.ReadCurveAsPolygon(tr, curve) as Polygon;
                    if (geom == null)
                        continue;

                    geom.UserData = objectId;
                    quadtree.Insert(geom.EnvelopeInternal, geom);
                    geometries.Add(geom);
                }

                foreach (var geom in geometries)
                {
                    var nearGeoms = GetNearGeometries(geom, quadtree, buffer);
                    var objectId = (ObjectId) geom.UserData;

                    var ids = new List<ObjectId>();
                    foreach (var geometry in nearGeoms)
                    {
                        var nearId = (ObjectId) geometry.UserData;
                        if (nearId != objectId)
                            ids.Add(nearId);
                    }

                    dictionary[objectId] = ids;
                }
                tr.Commit();
            }

            return dictionary;
        }

        public static Dictionary<Curve, IList<Curve>> GetNearGeometries(IEnumerable<Curve> curves, double buffer, bool forceClosed = true)
        {
            var dictionary = new Dictionary<Curve, IList<Curve>>();
            var quadtree = new Quadtree<IGeometry>();
            var reader = new DwgReader();
            var geometries = new List<IGeometry>();
            foreach (var curve in curves)
            {
                // 目前只处理封闭的和大于3的多边形
                // http://192.168.0.6:8080/browse/LCLIBCAD-903
                // 否则会弹出“points must form a closed linestring”异常
                if (forceClosed && !curve.Closed || !reader.NumberOfVerticesMoreThan3(curve))
                    continue;
                var geom = reader.ReadCurveAsPolygon(null, curve) as Polygon;
                if (geom == null)
                    continue;

                geom.UserData = curve;
                quadtree.Insert(geom.EnvelopeInternal, geom);
                geometries.Add(geom);
            }
            foreach (var geom in geometries)
            {
                var nearGeoms = GetNearGeometries(geom, quadtree, buffer);
                var curve = (Curve)geom.UserData;

                var nearCurves = new List<Curve>();
                foreach (var geometry in nearGeoms)
                {
                    var nearCurve = (Curve)geometry.UserData;
                    if (nearCurve != curve)
                        nearCurves.Add(nearCurve);
                }

                dictionary[curve] = nearCurves;
            }
            return dictionary;
        }

        //  If I'm not mistaken, you'll get 0 if you use DistanceOp on any polygon that intersects/contains the door polygon.
        //  I'd 
        //- create a small simple buffer on the door polygon,
        //- use PreparedPolygonFactory.Prepare on that (preparedDoor) and
        //- test all exterior rings of the polygons queried form the quadtree for intersection
        //Here is the pseudo code:
        public static List<IGeometry> GetNearGeometries(IGeometry me, Quadtree<IGeometry> others, double buffer)
        {
            var bufferedGeom = me.Buffer(buffer /*meter*/, 2, EndCapStyle.Flat);
            var preparedGeometry = PreparedGeometryFactory.Prepare(bufferedGeom);
            var geometries = new List<IGeometry>();
            foreach (var geom in others.Query(bufferedGeom.EnvelopeInternal))
            {
                var poly = geom as IPolygon;
                if (poly != null && preparedGeometry.Intersects(poly.ExteriorRing))
                    geometries.Add(poly);
            }
            return geometries;
        }

        public static void FindTouchedEdge(ObjectId[] polylineIds)
        {
            if (polylineIds.Length != 2)
                return;

            //var ids = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();

                var entity1 = tr.GetObject(polylineIds[0], OpenMode.ForRead) as Curve;
                var lineString1 = reader.ReadCurveAsLineString(tr, entity1);
                lineString1.UserData = polylineIds[0];

                var entity2 = tr.GetObject(polylineIds[1], OpenMode.ForRead) as Curve;
                var lineString2 = reader.ReadCurveAsLineString(tr, entity2);
                lineString2.UserData = polylineIds[1];

                var isTouch = lineString1.Touches(lineString2);
                System.Diagnostics.Trace.WriteLine(isTouch);

                IGeometry intersections = lineString1.Intersection(lineString2);

                //IGeometry intersections = CommonUtils.GetIntersectionPoints(lineString, polygon1);
                if (intersections.Coordinates.Any())
                {
                    foreach (var Coordinate in intersections.Coordinates)
                    {
                        var dbpoint = new DBPoint(new Point3d(Coordinate.X, Coordinate.Y, 0));
                        CadUtils.DrawPoint(tr, database, dbpoint);
                    }

                    // Draw all
                    var d = new DBPoint(new Point3d(intersections.Coordinates[0].X, intersections.Coordinates[0].Y, 0));
                    CadUtils.DrawPoint(tr, database, d, 2);

                    var lastIndex = intersections.Coordinates[intersections.Coordinates.Length - 1];
                    var d1 = new DBPoint(new Point3d(lastIndex.X,
                        lastIndex.Y, 0));
                    CadUtils.DrawPoint(tr, database, d1, 2);
                }

                tr.Commit();
            }
        }

        public static void FindDanglingLine(Database database)
        {
            var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var pmFixed3 = new PrecisionModel(3);
                // 读入多边形数据
                var lineStringList = new List<IGeometry>();
                foreach (ObjectId polylineId in polylineIds)
                {
                    var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;

                    var reader = new DwgReader();
                    IGeometry lineString;

                    try
                    {
                        lineString = reader.ReadCurveAsPolygon(tr, curve) as Polygon;
                    }
                    catch (Exception)
                    {
                        lineString = reader.ReadCurveAsLineString(tr, curve) as LineString;
                    }

                    if (lineString != null && lineString.IsEmpty == false)
                    {
                        lineString = SimpleGeometryPrecisionReducer.Reduce(lineString, pmFixed3);
                        lineStringList.Add(lineString);
                    }
                }

                // 开始做Union
                var nodedLineString = UnaryUnionOp.Union(lineStringList);
                var polygonizer = new Polygonizer();
                polygonizer.Add(nodedLineString);

                var polys = polygonizer.GetPolygons();
                var dangles = polygonizer.GetDangles();
                var cuts = polygonizer.GetCutEdges();

                var writer = new DwgWriter();
                var modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var blockTableRecord = (BlockTableRecord) tr.GetObject(modelSpaceId, OpenMode.ForWrite, false);

                // 悬挂线
                foreach (ILineString lineString in dangles)
                {
                    //if (lineString != null)
                    //{
                    //    var polyline = writer.WritePolyline(lineString);
                    //    polyline.ColorIndex = 3;
                    //    //polyline.Layer = "";
                    //    // 输出到CAD
                    //    blockTableRecord.AppendEntity(polyline);
                    //    tr.AddNewlyCreatedDBObject(polyline, true);
                    //}
                }

                tr.Commit();
            }
        }

        public static List<ObjectId> PolygonizeLineStrings(Database database, IEnumerable<ObjectId> polylineIdCollection,
            String outLayerName = "0",
            AcadColor color = null, double precision = 0.00001)
        {
            var result = new List<ObjectId>();
            using (var tr = database.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var lineStringList = new List<IGeometry>();
                foreach (ObjectId polylineId in polylineIdCollection)
                {
                    var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;

                    var reader = new DwgReader();
                    var lineString = reader.ReadGeometry(curve, tr) as LineString;

                    if (lineString != null && lineString.IsEmpty == false)
                    {
                        lineStringList.Add(lineString);
                    }
                }

#if TryManualUnion
                // 创建节点
                var nodedContours = new List<List<IGeometry>>();
                var scale = 1.0/precision;
                var geomNoder = new NetTopologySuite.Noding.Snapround.GeometryNoder(new PrecisionModel(scale));

                var nodedList = geomNoder.Node(lineStringList);
                // 这里可能可以用nodedList.Count来计算一个gourp的size来限定线程数量
                var maxCountInGroup = 200;

                foreach (var c in nodedList)
                {
                    //Coordinate[] linePts = CoordinateArrays.RemoveRepeatedPoints(c.Coordinates);
                    //if (linePts.Count() > 1)
                    //    nodedContours.Add(c.Factory.CreateLineString(linePts));
                    //c.Buffer(0.001);
                    // 将所有的线段每maxCountInGroup个分成一组.
                    var groupCount = nodedContours.Count;
                    if (groupCount == 0)
                    {
                        nodedContours.Add(new List<IGeometry>());
                    }
                    var itemCount = nodedContours[nodedContours.Count - 1].Count;
                    if (itemCount < maxCountInGroup)
                        nodedContours[nodedContours.Count - 1].Add(c);
                    else
                    {
                        nodedContours.Add(new List<IGeometry>());
                        nodedContours[nodedContours.Count - 1].Add(c);
                    }
                }

                var workers = new List<Worker>();
                var threadList = new List<Thread>();
                // 为每组geometry开一个线程做union.
                foreach (List<IGeometry> nodedContour in nodedContours)
                {
                    // Start a thread.
                    var worker = new Worker(nodedContour);
                    workers.Add(worker);
                    var thread = new Thread(worker.Execute);
                    threadList.Add(thread);
                    thread.Start();
                }

                // 等待所有线程运行结束
                foreach (Thread thread in threadList)
                {
                    thread.Join();
                }

                // 最后将每组union得到的IGeometry再做一次union
                var nodedLineString = workers[0].Geometry;
                for (int i = 1; i < workers.Count; i++)
                {
                    nodedLineString = nodedLineString.Union(workers[i].Geometry);
                }
#else

    // 开始做Union
                var nodedLineString = UnaryUnionOp.Union(lineStringList, new GeometryFactory(new PrecisionModel(0.9d)));

                //var nodedLineString = lineStringList[0];
                //for (int i = 1; i < lineStringList.Count; i++)
                //{
                //    nodedLineString = nodedLineString.Union(lineStringList[i]);
                //}
#endif

                //造区
                Polygonizer polygonizer = new Polygonizer();
                polygonizer.Add(nodedLineString);

                var polys = polygonizer.GetPolygons();
                var dangles = polygonizer.GetDangles();
                var cuts = polygonizer.GetCutEdges();
                var writer = new DwgWriter();
                var modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var blockTableRecord = (BlockTableRecord) tr.GetObject(modelSpaceId, OpenMode.ForWrite, false);
                // 多边形
                foreach (IGeometry geometry in polys)
                {
                    var polygon = geometry as Polygon;
                    if (polygon != null)
                    {
                        var polylines = writer.WritePolyline(polygon);
                        foreach (Polyline polyline in polylines)
                        {
                            if (color != null)
                                polyline.Color = color;
                            polyline.Layer = outLayerName;
                            // 输出到CAD
                            var polylineId = blockTableRecord.AppendEntity(polyline);
                            result.Add(polylineId);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                        }
                    }
                }

                //// 悬挂线
                //foreach (ILineString lineString in dangles)
                //{
                //    if (lineString != null)
                //    {
                //        var polyline = writer.WritePolyline(lineString);
                //        if (color != null)
                //            polyline.Color = color;
                //        polyline.Layer = outLayerName;
                //        // 输出到CAD
                //        blockTableRecord.AppendEntity(polyline);
                //        tr.AddNewlyCreatedDBObject(polyline, true);
                //    }
                //}

                //// 裁剪线
                //foreach (ILineString lineString in cuts)
                //{
                //    if (lineString != null)
                //    {
                //        var polyline = writer.WritePolyline(lineString);
                //        if (color != null)
                //            polyline.Color = color;
                //        polyline.Layer = outLayerName;
                //        // 输出到CAD
                //        blockTableRecord.AppendEntity(polyline);
                //        tr.AddNewlyCreatedDBObject(polyline, true);
                //    }
                //}

                tr.Commit();
            }
            return result;
        }

        public static Point3d? GetCentroid(ObjectId polylineId)
        {
            Point3d? result = null;
            using (var transaction = polylineId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity) transaction.GetObject(polylineId, OpenMode.ForRead);
                result = GetCentroid(entity, transaction);
                transaction.Commit();
            }
            return result;
        }

        public static Point3d? GetCentroid(Entity entity, Transaction tr)
        {
            Point3d? result = null;
            var reader = new DwgReader();
            var geomerty = reader.ReadGeometry(entity, tr);
            var coords = NetTopologySuite.Algorithm.Centroid.GetCentroid(geomerty);
            if (coords != null)
                result = new Point3d(coords.X, coords.Y, coords.Z);
            return result;
        }

        public static Point3d? GetCentroid(IList<Point3d> points)
        {
            Point3d? result = null;
            var coordinates = new List<Coordinate>();
            foreach (var point in points)
            {
                var coordinate = new Coordinate()
                {
                    X = point.X,
                    Y = point.Y,
                    Z = 0
                };
                coordinates.Add(coordinate);
            }
            ICoordinateSequence coordinateSequence = CoordinateArraySequenceFactory.Instance.Create(coordinates.ToArray());

            var geometryF = new DwgWriter();
            var polygon = geometryF.GeometryFactory.CreatePolygon(coordinateSequence.ToCoordinateArray());
            var coords = Centroid.GetCentroid(polygon);
            if (coords != null)
                result = new Point3d(coords.X, coords.Y, coords.Z);
            return result;
        }

        public static IList<Point3d> GetMinimumDiameterRectangle(Entity entity, Transaction tr)
        {
            var reader = new DwgReader();
            var geomerty = reader.ReadGeometry(entity, tr);
            var diameter = new NetTopologySuite.Algorithm.MinimumDiameter(geomerty);
            var rectangle = diameter.GetMinimumRectangle();

            var coordinates = new List<Point3d>();
            foreach (var coordinate in rectangle.Coordinates)
            {
                var point = new Point3d(coordinate.X, coordinate.Y, 0);
                coordinates.Add(point);
            }
            return coordinates;
        }

        public static Extents3d? GetExtent(IList<Point3d> points)
        {
            var coordinates = new List<Coordinate>();
            foreach (var point in points)
            {
                var coordinate = new Coordinate()
                {
                    X = point.X,
                    Y = point.Y,
                    Z = 0
                };
                coordinates.Add(coordinate);
            }
            ICoordinateSequence coordinateSequence = CoordinateArraySequenceFactory.Instance.Create(coordinates.ToArray());
            var geometryF = new DwgWriter();
            var polygon = geometryF.GeometryFactory.CreatePolygon(coordinateSequence.ToCoordinateArray());
            var envelop = (Envelope)polygon.Envelope;
            var extent3D = new Extents3d(new Point3d(envelop.MinX, envelop.MinY, 0), 
                new Point3d(envelop.MaxX, envelop.MaxY, 0));
            return extent3D;
        }

        public static bool IsPolylineCountClockWise(ObjectId plineId)
        {
            using (var tr = plineId.Database.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(plineId, OpenMode.ForRead) as Curve;
                if (pline != null)
                {
                    return IsCcw(pline, tr);
                }
            }
            return false;
        }

        public static bool IsCcw(Curve curve, Transaction tr)
        {
            var reader = new DwgReader();
            var geomerty = reader.ReadCurveAsLineString(tr, curve);

            /*
             * This check catches cases where the ring contains an A-B-A configuration of points.
             * This can happen if the ring does not contain 3 distinct points
             * (including the case where the input array has fewer than 4 elements),
             * or it contains coincident line segments.
             */
            if (geomerty.CoordinateSequence.Count < 3) // To avoid exception from CGAlgorithms.IsCCW routine
                return false;

            return CGAlgorithms.IsCCW(geomerty.CoordinateSequence);
        }

        public static bool IsCcw(IList<Point2d> points)
        {
            var coordinates = new List<Coordinate>();
            foreach (var point2D in points)
            {
                var coordinate = new Coordinate()
                {
                    X = point2D.X,
                    Y = point2D.Y,
                    Z = 0
                };
                coordinates.Add(coordinate);
            }
            ICoordinateSequence coordinateSequence = CoordinateArraySequenceFactory.Instance.Create(coordinates.ToArray());
            return CGAlgorithms.IsCCW(coordinateSequence);
        }

        public static void TestUnionPolygonCascaded()
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
            var selectionOpts = new PromptSelectionOptions();

            var currDoc = Application.DocumentManager.MdiActiveDocument;
            //选择polyline
            PromptSelectionResult result = currDoc.Editor.GetSelection(selectionOpts, filter);
            if (result.Status == PromptStatus.OK)
            {
                if (result.Value.Count > 1)
                {
                    var objectIds = result.Value.GetObjectIds();
                    UnionPolygonCascaded(objectIds);
                }
                else
                {
                    currDoc.Editor.WriteMessage("\n选择至少两个相邻的多段线");
                }
            }
        }
        
        public static ObjectId UnionPolygonCascaded(ObjectId[] plineIds)
        {
            if (plineIds.Count() == 0)
                return ObjectId.Null;

            var deletePolyIds = new List<ObjectId>();
            var database = plineIds[0].Database;
            var geometries = new List<IGeometry>();
            ObjectId polygonId = ObjectId.Null;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader() { PrecisionScale = 100000 };
                foreach (var plineId in plineIds)
                {
                    var pline = tr.GetObject(plineId, OpenMode.ForRead) as Curve;
                    if (pline != null && pline.Closed)
                    {
                        var geomerty = reader.ReadCurveAsPolygon(tr, pline);
                        geomerty.Buffer(0);
                        geometries.Add(geomerty);
                        deletePolyIds.Add(plineId);
                    }
                }

                // 合并
                var resultPolygon = CascadedPolygonUnion.Union(geometries);
                var writer = new DwgWriter() { PrecisionScale = 100000 };
                var polygon = resultPolygon as Polygon;
                if (polygon != null)
                {
                    var polyline = writer.WritePolyline(polygon.Shell);

                    var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                    var modelspace = (BlockTableRecord) tr.GetObject(modelspaceId, OpenMode.ForWrite);
                    polygonId = modelspace.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);

                    // 删除已经形成多边形的地块
                    foreach (var plineId in deletePolyIds)
                    {
                        var pline = tr.GetObject(plineId, OpenMode.ForWrite) as Curve;
                        var geomerty = reader.ReadCurveAsPolygon(tr, pline);
                        if (pline != null && geomerty !=null && polygon.Contains(geomerty))
                        {
                            pline.Erase(true);
                        }
                    }
                }
                tr.Commit();
            }

            return polygonId;
        }

        static IGeometry Polygonize(IGeometry geometry)
        {
            var lines = LineStringExtracter.GetLines(geometry);
            var polygonizer = new Polygonizer();
            polygonizer.Add(lines);
            var polys = polygonizer.GetPolygons();
            var polyArray = GeometryFactory.ToGeometryArray(polys);
            return geometry.Factory.CreateGeometryCollection(polyArray);
        }

        static IGeometry SplitPolygon(IGeometry polygon, IGeometry line)
        {
            var nodedLinework = polygon.Boundary.Union(line);
            var polygons = Polygonize(nodedLinework);

            // only keep polygons which are inside the input
            var output = new List<IGeometry>();
            for (var i = 0; i < polygons.NumGeometries; i++)
            {
                var candpoly = (IPolygon)polygons.GetGeometryN(i);
                if (polygon.Contains(candpoly.InteriorPoint))
                    output.Add(candpoly);
            }
            return polygon.Factory.BuildGeometry(output);
        }

        public static List<ObjectId> SplitPolygon(ObjectId plineId, ObjectId lineId)
        {
            var polyIds = new List<ObjectId>();
            if (plineId.IsNull && lineId.IsNull)
                return polyIds;
            
            var database = plineId.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader() { PrecisionScale = 100000 };
                var pline = tr.GetObject(plineId, OpenMode.ForRead) as Curve;
                if (pline == null || !pline.Closed)
                    return polyIds;

                var lineObj = tr.GetObject(lineId, OpenMode.ForRead) as Curve;
                if (lineObj == null)
                    return polyIds;

                var splitGeometry = reader.ReadCurveAsPolygon(tr, pline);
                splitGeometry.Buffer(0);

                var lineGeometry = reader.ReadGeometry(lineObj, tr);
                lineGeometry.Buffer(0);

                // 合并
                var resultPolygons = SplitPolygon(splitGeometry, lineGeometry);
                var writer = new DwgWriter() { PrecisionScale = 100000 };
                var multiPolygon = resultPolygons as MultiPolygon;
                if (multiPolygon != null)
                {
                    var polylines = writer.WritePolyline(multiPolygon);

                    // 加入数据库
                    foreach (var polyline in polylines)
                    {
                        var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                        var modelspace = (BlockTableRecord)tr.GetObject(modelspaceId, OpenMode.ForWrite);
                        var polyId = modelspace.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                        polyIds.Add(polyId);
                    }
                }
                tr.Commit();
            }

            return polyIds;
        }
    }

#if TryManualUnion
    class Worker
    {
        private readonly IList<IGeometry> _geoms;

        private IGeometry _ret;

        protected internal Worker(IList<IGeometry> geoms)
        {
            _geoms = geoms;
        }

        internal void Execute()
        {
            _ret = _geoms[0];
            for (int i = 1; i < _geoms.Count; i++)
            {
                _ret = _ret.Union(_geoms[i]);
            }
        }

        public IGeometry Geometry
        {
            get { return _ret; }
        }
    }
#endif
}
