using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DbxUtils.Utils;
using GeoAPI.Geometries;
using NetTopologySuite.Index.Quadtree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TopologyTools.ReaderWriter;

namespace TopologyTools.Utils
{
    public sealed class OperationTimer : IDisposable
	{
		readonly string m_operation;
		readonly Stopwatch m_stopWatch;

		public OperationTimer(string operation)
		{
			m_operation = operation;

			m_stopWatch = new Stopwatch();
			m_stopWatch.Start();
		}

        public void Stop()
        {
            m_stopWatch.Stop();
        }

        public TimeSpan ElapsedTime
	    {
            get { return new TimeSpan(m_stopWatch.ElapsedTicks); }
	    }

	    public string ElapsedTimeMessage
	    {
	        get
	        {
                //return String.Format(CultureInfo.InvariantCulture,
                //    "耗时{0}小时{1}分{2}秒{3}毫秒\n", ElapsedTime.Hours, ElapsedTime.Minutes, ElapsedTime.Seconds, ElapsedTime.Milliseconds);
                return String.Format(CultureInfo.InvariantCulture,
                    "耗时{0}秒\n", ElapsedTime.TotalSeconds);
	        }
	    }

		/// <summary>
		/// Report the elapsed time and stop the stopwatch.
		/// </summary>
		public void Dispose()
		{
			if (!m_stopWatch.IsRunning) return;

			m_stopWatch.Stop();
			System.Diagnostics.Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0} time: {1} ms", m_operation, m_stopWatch.ElapsedMilliseconds));
		}

	    public Func<string, bool> PostDispose { get; set; }
	}

    public class PolygonHoleHelper
    {
        public static string CassFlagIsland = "340099"; // 孔洞，与cass农经版相同
        public static string CassFlagName = "300000";

        //[CommandMethod("SDKCTX")]
        public static void FindRemoveAreaPolyline()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            var holes = FindPotentialHoles(document);
            if (!holes.Any())
            {
                document.Editor.WriteMessage("不存在孔洞多边形");
                return;
            }
            document.Editor.WriteMessage("查找到{0}个孔洞多边形", holes.Count);

            var holeIds = holes.ToArray();

            Extents3d? extents = CadUtils.SafeGetGeometricExtents(holeIds);
            if (extents.HasValue)
                CadUtils.ZoomToWin1(document.Editor, extents.Value, 1.2);

            Autodesk.AutoCAD.Internal.Utils.SelectObjects(holeIds);

            //var ptopts = new PromptKeywordOptions("\n是否将这些多边形设定为扣除面积的图形(Y)？[是(Y)/否(N)]:", "Yes No") { AllowNone = true };
            //var ptRes = document.Editor.DoPrompt(ptopts);

            //if (ptRes.Status == PromptStatus.OK && ptRes.StringResult == "Yes")
            //{
            //    SetHolePolygons(document.Database, holeIds);
            //}
        }

        public static void SelectHolePolygons()
        {
            var objectIds = new ObjectId[0];
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            objectIds = ed.GetSelection().Value.GetObjectIds();
            if (objectIds.Count() == 0)
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
                if (result.Status == PromptStatus.OK || result.Value.Count >= 1)
                {
                    objectIds = result.Value.GetObjectIds();
                }
            }

            // 
            if (objectIds.Count() > 0)
            {
                var count = objectIds.Count();
                Document document = Application.DocumentManager.MdiActiveDocument;
                //int count = SetHolePolygons(document.Database, objectIds);
                document.Editor.WriteMessage("将{0}个多边形设定为扣除面积的图形", count);
            }
        }

        public static void SelectHoleToRemoveArea()
        {
            var holeIds = new ObjectId[0];
            ObjectId parcelId = ObjectId.Null;
            Document document = Application.DocumentManager.MdiActiveDocument;

            document.Editor.WriteMessage("\n选择需要扣除面积的孔洞: ");
            var result = StartSelectPolyline(false);
            if (result.Status != PromptStatus.OK)
                return;

            // 洞
            if (result.Value.Count >= 1)
            {
                holeIds = result.Value.GetObjectIds();
            }

            // 检查地块是否封闭
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in holeIds)
                {
                    var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null && !curve.Closed)
                    {
                        document.Editor.WriteMessage("\n错误：选择的孔洞不封闭");
                        return;
                    }
                }
            }

            // 外边界
            Point3d pickedPoint;
            if (!SelectPolylineEntity("\n选择要扣除面积的地块界线: ", "\n实体错误", out parcelId, out pickedPoint)
                || parcelId == ObjectId.Null) 
                return;

            // 检查地块是否封闭
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                var curve = tr.GetObject(parcelId, OpenMode.ForRead) as Curve;
                if (curve != null && !curve.Closed)
                {
                    document.Editor.WriteMessage("\n错误：选择的地块界线不封闭");
                    return;
                }
            }

            // 如果地块是合适的
            if (parcelId.IsValid && holeIds.Any())
            {
                foreach (var objectId in holeIds)
                {
                    if (objectId == parcelId)
                    {
                        document.Editor.WriteMessage("\n错误：句柄为{0}的孔洞与地块边界相同", objectId.Handle);
                        return;
                    }

                    if (!WithIn(objectId, parcelId))
                    {
                        document.Editor.WriteMessage("\n错误：句柄为{0}的孔洞不在地块边界内部", objectId.Handle);
                        return;
                    }
                }

                // 新增孔洞到地块
                var parcel = new ParcelPolygon(parcelId);
                parcel.AddHoleIds(holeIds);
                document.Editor.WriteMessage("\n将{0}个多边形设定为扣除面积的孔洞", holeIds.Count());
            }
        }

        /// <summary>
        /// Wrapper the select polyline entity function
        /// </summary>
        public static bool SelectPolylineEntity(string promptMessage, string rejectMesage, out ObjectId entityId, out Point3d pickPoint)
        {
            entityId = ObjectId.Null;
            pickPoint = Point3d.Origin;

            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var peo = new PromptEntityOptions(promptMessage);
            peo.SetRejectMessage(rejectMesage);
            peo.AllowNone = false;
            peo.AllowObjectOnLockedLayer = false;
            peo.AddAllowedClass(typeof(Polyline), true);
            peo.AddAllowedClass(typeof(Polyline2d), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status == PromptStatus.OK)
            {
                entityId = per.ObjectId;
                pickPoint = per.PickedPoint;
                return true;
            }

            return false;
        }

        static PromptSelectionResult StartSelectPolyline(bool singleOnly)
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
            var selectionOpts = new PromptSelectionOptions() { SingleOnly = singleOnly, };
            var currDoc = Application.DocumentManager.MdiActiveDocument;
            //选择polyline
            PromptSelectionResult result = currDoc.Editor.GetSelection(selectionOpts, filter);
            return result;
        }

        public static int SetHolePolygons(Database database, ObjectId[] holeIds)
        {
            int count = 0;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in holeIds)
                {
                    var hole = tr.GetObject(objectId, OpenMode.ForWrite) as Polyline;
                    if (hole != null)
                    {
                        CadUtils.SetCassFlag(hole, CassFlagIsland);
                        count++;
                    }
                }
                // Saves the changes to the database and closes the transaction
                tr.Commit();
            }

            return count;
        }

        public static List<ObjectId> FindHolesByFlag(Document document)
        {
            var polylineIds = CadUtils.FindAllPolylines(document);
            var datebase = document.Database;
            var objectIds = new List<ObjectId>();
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                foreach (var objectId in polylineIds)
                {
                    var hole = tr.GetObject(objectId, OpenMode.ForRead) as Entity;
                    if (CadUtils.GetCassFlag(hole) == CassFlagIsland)
                        objectIds.Add(objectId);
                }
                // Saves the changes to the database and closes the transaction
                tr.Commit();
            }

            return objectIds;
        }

        public static bool HasHoles(Document document)
        {
            var polylineIds = CadUtils.FindAllPolylines(document);
            var datebase = document.Database;
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                foreach (var objectId in polylineIds)
                {
                    var hole = tr.GetObject(objectId, OpenMode.ForRead) as Entity;
                    if (CadUtils.GetCassFlag(hole) == CassFlagIsland)
                        return true;
                }
                // Saves the changes to the database and closes the transaction
                tr.Commit();
            }

            return false;
        }

        public static bool WithIn(ObjectId hole, ObjectId pacelId)
        {
            using (var tr = hole.Database.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var reader = new DwgReader();
                var polygon = reader.ReadEntityAsPolygon(tr, hole) as IPolygon;
                var polygon2 = reader.ReadEntityAsPolygon(tr, pacelId) as IPolygon;
                if (polygon != null && polygon2 != null)
                {
                    return polygon.Within(polygon2);
                }
            }
            return false;
        }

        public static List<ObjectId> FindPotentialHoles(Document document)
        {
            var polylineIds = CadUtils.FindAllPolylines(document);
            var datebase = document.Database;
            var hashSetObjIds = new HashSet<ObjectId>(); // 避免重复的数据，用hashset
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var reader = new DwgReader();
                var polygons = new List<IPolygon>();
                var quadtree = new Quadtree<IGeometry>();

                var possibleHoleIds = new List<ObjectId>();
                foreach (ObjectId polylineId in polylineIds)
                {
                    var polygon = reader.ReadEntityAsPolygon(tr, polylineId) as IPolygon;
                    if (polygon != null)
                    {
                        polygons.Add(polygon);
                        quadtree.Insert(polygon.EnvelopeInternal, polygon);
                    }

                    // ObjectIds
                    using (var ent = tr.GetObject(polylineId, OpenMode.ForRead) as Entity)
                    {
                        if (ent is Polyline2d || ent is Polyline)
                        {
                            // 如果是地块，直接跳过
                            string cassFlag = CadUtils.GetCassFlag(ent);
                            if (CassFlagName.ToLower() != cassFlag.ToLower())
                            {
                                possibleHoleIds.Add(polylineId);
                            }
                        }
                    }
                }

                // 遍历多边形，如果有洞，开始计算 
                foreach (var polygon in polygons)
                {
                    //看看是否包含洞
                    //quadtree.Query()

                    // 找洞
                    foreach (var geom in quadtree.Query(polygon.EnvelopeInternal))
                    {
                        var hole = geom as IPolygon;
                        if (hole == null)
                            continue;

                        var holeId = (ObjectId) hole.UserData;
                        if (possibleHoleIds.Contains(holeId) // 不是潜在的地块
                            && !hole.Equals(polygon) // 不是同一个多边形
                            && hole.UserData != polygon.UserData // 不是自己
                            && hole.Within(polygon))  // 有洞！
                        {
                            hashSetObjIds.Add(holeId); // 
                        }
                    }
                }
                tr.Commit();
                return hashSetObjIds.ToList();
            }
        }

        public static bool IsHoleReferenced(ObjectId holeId)
        {
            using (var tr = holeId.Database.TransactionManager.StartTransaction())
            {
                using (var ent = tr.GetObject(holeId, OpenMode.ForRead) as Entity)
                {
                    var referenced = IsHoleReferenced(ent, tr);
                    tr.Commit();
                    return referenced;
                }
            }
        }

        public static bool IsHoleReferenced(Transaction tr, ObjectId holeId)
        {
            using (var ent = tr.GetObject(holeId, OpenMode.ForRead) as Entity)
            {
                return IsHoleReferenced(ent, tr);
            }
        }

        public static bool IsHoleReferenced(Entity ent, Transaction tr)
        {
            if (ent is Polyline2d || ent is Polyline)
            {
                // 如果不是flag，没有被引用
                string cassFlag = CadUtils.GetCassFlag(ent);
                if (CassFlagIsland.ToLower() != cassFlag.ToLower())
                    return false;

                var groupId = GroupUtils.FindEntityGroupId(tr, ent);
                if (!groupId.IsValid)
                    return false;

                // 如果不是flag，没有被引用
                //ObjectId[] entityIds = GroupUtils.GetGroupedObjects(ent);
                //foreach (ObjectId entityId in entityIds)
                //{

                //}
                var realId = ParcelPolygon.FindParcelOfHole(tr, ent);
                if (realId.IsNull)
                    return false;

                return true;
            }

            return false;
        }

        public static List<ObjectId> FindUnreferenceHoles(Document document)
        {
            var holes = FindPotentialHoles(document);

            var holeIds = new List<ObjectId>();
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in holes)
                {
                    if (!IsHoleReferenced(tr, objectId))
                        holeIds.Add(objectId);
                }

                tr.Commit();
            }

            return holeIds;
        }

        public static double FindHoleArea(ObjectId parcelId)
        {
            var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var datebase = Application.DocumentManager.MdiActiveDocument.Database;
            var dictionary = new Dictionary<ObjectId, double>();
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var reader = new DwgReader();
                var polygons = new List<IPolygon>();
                var quadtree = new Quadtree<IGeometry>();

                foreach (ObjectId polylineId in polylineIds)
                {
                    var polygon = reader.ReadEntityAsPolygon(tr, polylineId) as IPolygon;
                    if (polygon != null)
                    {
                        polygons.Add(polygon);
                        quadtree.Insert(polygon.EnvelopeInternal, polygon);
                    }
                }

                // 遍历多边形，如果有洞，开始计算 
                foreach (var polygon in polygons)
                {
                    // 找洞
                    var insidePolygons = new List<IPolygon>();
                    foreach (var geom in quadtree.Query(polygon.EnvelopeInternal))
                    {
                        var insidePolygon = geom as IPolygon;
                        if (insidePolygon != null
                            && polygon.Contains(insidePolygon)
                            && !insidePolygon.Equals(polygon) // 不是同一个
                            && insidePolygon.UserData != polygon.UserData)
                        {
                            insidePolygons.Add(insidePolygon);
                        }
                    }

                    // 算面积
                    var polygonId = (ObjectId)polygon.UserData;
                    var linearRings = new List<ILinearRing>();
                    if (insidePolygons.Any())
                    {
                        foreach (var insidePolygon in insidePolygons)
                        {
                            ILinearRing linearRing = reader.GeometryFactory.CreateLinearRing(insidePolygon.ExteriorRing.CoordinateSequence);
                            if (!linearRing.IsCCW)
                                linearRing.Reverse();
                            linearRings.Add(linearRing);
                        }

                    }
                    IPolygon newPolygon = reader.GeometryFactory.CreatePolygon(polygon.Shell, linearRings.ToArray());
                    dictionary.Add(polygonId, newPolygon.Area);
                }
                tr.Commit();
            }

            return dictionary[parcelId];
        }

        public static List<ObjectId> FindHoles()
        {
            var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var datebase = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var reader = new DwgReader();
                var polygons = new List<IPolygon>();
                var quadtree = new Quadtree<IGeometry>();

                foreach (ObjectId polylineId in polylineIds)
                {
                    var polygon = reader.ReadEntityAsPolygon(tr, polylineId) as IPolygon;
                    if (polygon != null)
                    {
                        polygons.Add(polygon);
                        quadtree.Insert(polygon.EnvelopeInternal, polygon);
                    }
                }

                var possibleHoleIds = new List<ObjectId>();

                // 便利多边形，如果有洞，开始计算 
                foreach (var polygon in polygons)
                {
                    // 找洞
                    foreach (var geom in quadtree.Query(polygon.EnvelopeInternal))
                    {
                        var insidePolygon = geom as IPolygon;
                        if (insidePolygon != null
                            && polygon.Within(insidePolygon)
                            && insidePolygon.UserData != polygon.UserData) // 不要是自己
                        {
                            possibleHoleIds.Add((ObjectId)insidePolygon.UserData);
                        }
                    }
                }
                tr.Commit();

                return possibleHoleIds;
            }
        }

        public class TopoData
        {
            public DwgReader Reader { get; set; }
            public Quadtree<IGeometry> Quadtree { get; set; }
            public Dictionary<ObjectId, IPolygon> Polygons { get; set; }
        }

        static TopoData BuildTopology()
        {
            var polylineIds = CadUtils.FindAllPolylines(Application.DocumentManager.MdiActiveDocument);
            var datebase = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = datebase.TransactionManager.StartTransaction())
            {
                // 读入多边形数据
                var reader = new DwgReader();
                var polygons = new Dictionary<ObjectId, IPolygon>();
                var quadtree = new Quadtree<IGeometry>();

                // 构建拓扑
                foreach (ObjectId polylineId in polylineIds)
                {
                    var polygon = reader.ReadEntityAsPolygon(tr, polylineId) as IPolygon;
                    if (polygon != null)
                    {
                        polygons.Add(polylineId, polygon);
                        quadtree.Insert(polygon.EnvelopeInternal, polygon);
                    }
                }

                tr.Commit();

                return new TopoData()
                {
                    Polygons = polygons,
                    Quadtree = quadtree,
                    Reader = reader,
                };
            }
        }

        static Dictionary<ObjectId, IList<ObjectId>> GetPolygonHasHole(Database database,
            Quadtree<IGeometry> quadtree, IEnumerable<IPolygon> polygons)
        {
            var dictionary = new Dictionary<ObjectId, IList<ObjectId>>();
            var possibleHoleIds = new List<ObjectId>();
            using (var tr = database.TransactionManager.StartTransaction())
            {
                // 便利多边形，如果有洞，开始计算 
                foreach (var polygon in polygons)
                {
                    // 找洞
                    var insidePolygons = new List<IPolygon>();
                    foreach (var geom in quadtree.Query(polygon.EnvelopeInternal))
                    {
                        var insidePolygon = geom as IPolygon;
                        if (insidePolygon != null)
                        {
                            var objectId = (ObjectId) insidePolygon.UserData;
                            var dbObj = tr.GetObject(objectId, OpenMode.ForRead);

                            // 必须是孔洞
                            if (CadUtils.GetCassFlag(dbObj) == CassFlagIsland)
                            {
                                if (polygon.Contains(insidePolygon)
                                    && !insidePolygon.Equals(polygon)
                                    && insidePolygon.UserData != polygon.UserData)
                                {
                                    insidePolygons.Add(insidePolygon);
                                    possibleHoleIds.Add(objectId);
                                }
                            }
                        }
                    }

                    // 算面积
                    var polygonId = (ObjectId) polygon.UserData;
                    if (insidePolygons.Any())
                    {
                        var objectIds = new List<ObjectId>();
                        foreach (var insidePolygon in insidePolygons)
                            objectIds.Add((ObjectId)insidePolygon.UserData);
                        dictionary.Add(polygonId, objectIds);
                    }
                }
            }

            return dictionary;
        }

        public static decimal GetPolygonArea(ObjectId objectId)
        {
            var parcelPolygon = new ParcelPolygon(objectId);
            return parcelPolygon.GetArea();
        }

        public static decimal GetPolygonArea(Transaction tr, Entity entity)
        {
            var parcelPolygon = new ParcelPolygon(tr, entity as Curve);
            return parcelPolygon.GetArea();
        }

        public class ParcelPolygon
        {
            public ParcelPolygon(ObjectId id)
            {
                ObjectId = id;
            }

            public ParcelPolygon(Transaction tr, Curve entity)
            {
                Transaction = tr;
                Curve = entity;
                ObjectId = entity.ObjectId;
            }

            public ObjectId ObjectId { get; set; }
            public Transaction Transaction { get; set; }
            public Curve Curve { get; set; }

            public bool ContainsHole(ObjectId holeId)
            {
                using (var tr = ObjectId.Database.TransactionManager.StartTransaction())
                {
                    var curve = tr.GetObject(ObjectId, OpenMode.ForRead) as Curve;
                    var hole = tr.GetObject(holeId, OpenMode.ForRead) as Curve;
                    if (curve != null && curve.Closed
                        && hole != null && hole.Closed)
                    {
                        var reader = new DwgReader();
                        var polygon1 = reader.ReadCurveAsPolygon(tr, curve);
                        var polygon2 = reader.ReadCurveAsPolygon(tr, hole);
                        if (polygon1.Contains(polygon2))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public IPolygon GetPolygon(Transaction tr)
            {
                var curve = tr.GetObject(ObjectId, OpenMode.ForRead) as Curve;
                if (curve != null && curve.Closed)
                {
                    var holeIds = FindHolesInEntity(tr, curve);
                    var reader = new DwgReader();
                    var polygon = reader.ReadCurveAsPolygon(tr, curve);
                    var linearRings = new List<ILinearRing>();
                    foreach (var holeId in holeIds)
                    {
                        // 处理洞
                        var insideCurve = tr.GetObject(holeId, OpenMode.ForRead) as Curve;
                        if (insideCurve != null && curve.Closed)
                        {
                            var insidePolygon = reader.ReadCurveAsPolygon(tr, insideCurve);
                            if (polygon.Contains(insidePolygon))
                            {
                                ILinearRing linearRing = reader.GeometryFactory.CreateLinearRing(insidePolygon.ExteriorRing.CoordinateSequence);
                                if (!linearRing.IsCCW)
                                    linearRing.Reverse();
                                linearRings.Add(linearRing);
                            }
                        }
                    }

                    if (!linearRings.Any())
                        return polygon;

                    // 返回新建的多边形
                    IPolygon newPolygon = reader.GeometryFactory.CreatePolygon(polygon.Shell, linearRings.ToArray());
                    return newPolygon;
                }
                return null;
            }

            public decimal GetArea()
            {
                if (Curve != null && Curve.Closed)
                {
                    if (!IsPolygonHasHole(Transaction, Curve))
                    {
                        double area = Curve.Area;
                        if (Double.IsNaN(area))
                            return 0;
                        return (decimal)area;
                    }

                    var polygon = GetPolygon(Transaction);
                    if (polygon != null)
                        return (decimal)polygon.Area;
                }
                return 0;
            }

            public void AddHoleId(ObjectId holeId)
            {
                var holeIds = new [] {holeId};
                AddHoleIds(holeIds);
            }

            public void AddHoleIds(ObjectId[] holeIds)
            {
                AddHolesToEntityGroup(ObjectId, holeIds);
            }

            public ObjectId[] GetHoleIds()
            {
                using (var tr = ObjectId.Database.TransactionManager.StartTransaction())
                {
                    using (var ent = tr.GetObject(ObjectId, OpenMode.ForRead) as Entity)
                    {
                        return FindHolesInEntity(tr, ent);
                    }
                    tr.Commit();
                }
                return new ObjectId[0];
            }

            static void AddHolesToEntityGroup(ObjectId parcelId, ObjectId[] holeIds)
            {
                // 看是否存在组，如果没有，直接新建一个
                var groupId = GroupUtils.FindEntityGroupId(parcelId);

                // 关联地块和孔洞
                AddHolesToParcel(parcelId, groupId, holeIds);
            }

            static void AddHolesToParcel(ObjectId parcelId, ObjectId groupId, ObjectId[] holeIds)
            {
                Database database = parcelId.Database;
                // 将扣除的孔洞等图形放到孔洞图层，以绿色醒目显示
                var layerId = LayerUtils.GetLayerByName(database, "孔洞");
                if (!layerId.IsValid)
                    layerId = LayerUtils.AddNewLayer(database, "孔洞", "Continuous", /*GREEN*/3);

                // 看看选择的地块是不是真的在地块里面了
                var availableHoles = new List<ObjectId>();
                using (var tr = database.TransactionManager.StartTransaction())
                {
                    var reader = new DwgReader();
                    var curve = tr.GetObject(parcelId, OpenMode.ForRead) as Curve;
                    if (curve != null && curve.Closed)
                    {
                        var polygon1 = reader.ReadCurveAsPolygon(tr, curve);
                        foreach (var holeId in holeIds)
                        {
                            var hole = tr.GetObject(holeId, OpenMode.ForRead) as Curve;
                            // 只添加地块为封闭的
                            if (hole == null || !hole.Closed)
                                continue;

                            // 继续，看看是不是在地块中间，不在就跳过
                            var polygon2 = reader.ReadCurveAsPolygon(tr, hole);
                            if (!polygon1.Contains(polygon2))
                                continue;

                            // 如果是，添加地块
                            hole.UpgradeOpen();
                            CadUtils.SetCassFlag(hole, CassFlagIsland);
                            hole.LayerId = layerId;

                            availableHoles.Add(holeId);

                            // 如果组不是空，直接加入组
                            if (!groupId.IsNull)
                                GroupUtils.AppendEntityIntoGroup(database, groupId, holeId);
                        }
                    }
                    tr.Commit();
                }

                // 如果没有group，创建group
                if (groupId.IsNull)
                {
                    var ids = new ObjectIdCollection() { parcelId };
                    foreach (var availableHole in availableHoles)
                        ids.Add(availableHole);
                    GroupUtils.CreateNewGroup(database, "*A", ids);
                }
            }

            public static ObjectId[] FindHolesInEntity(Transaction tr, Entity entity)
            {
                ObjectId[] entityIds = GroupUtils.GetGroupedObjects(tr, entity);

                var holeIds = new List<ObjectId>();
                foreach (ObjectId entityId in entityIds)
                {
                    using (var ent = tr.GetObject(entityId, OpenMode.ForRead))
                    {
                        if (ent is Polyline2d || ent is Polyline)
                        {
                            string cassFlag = CadUtils.GetCassFlag(ent);
                            if (CassFlagIsland.ToLower() == cassFlag.ToLower())
                                holeIds.Add(entityId);
                        }
                    }
                }

                return holeIds.ToArray();
            }

            public static ObjectId FindParcelOfHole(Transaction tr, Entity hole)
            {
                var entityIds = GroupUtils.GetGroupedObjects(tr, hole);
                ObjectId entityId = ObjectId.Null;
                foreach (var objectId in entityIds)
                {
                    if (objectId == hole.Id)
                        continue;

                    using (var ent = tr.GetObject(objectId, OpenMode.ForRead))
                    {
                        if (ent is Polyline2d || ent is Polyline)
                        {
                            string cassFlag = CadUtils.GetCassFlag(ent);
                            if (CassFlagName.ToLower() == cassFlag.ToLower())
                            {
                                entityId = objectId;
                                break;
                            }
                        }
                    }
                }
                return entityId;
            }

            public static void RemoveUnReferenceHoles()
            {
                var document = Application.DocumentManager.MdiActiveDocument;
                var holeIds = FindUnreferenceHoles(document);
                if (!holeIds.Any())
                {
                    document.Editor.WriteMessage("\n 没有引用错误的孔洞");
                    return;
                }

                CleanupSelfRefHoles(holeIds.ToArray());
            }

            public static void CleanupSelfRefHoles(ObjectId[] holeIds)
            {
                var database = holeIds[0].Database;
                using (var tr = database.TransactionManager.StartTransaction())
                {
                    foreach (var holeId in holeIds)
                    {
                        var hole = database.TransactionManager.GetObject(holeId, OpenMode.ForRead) as Entity;
                        var col = hole.GetPersistentReactorIds();
                        if (col != null)
                        {
                            foreach (ObjectId id in col)
                            {
                                using (DBObject obj = tr.GetObject(id, OpenMode.ForRead))
                                {
                                    if (obj is Group)
                                    {
                                        var group = obj as Group;
                                        group.UpgradeOpen();
                                        var objectIds = GroupUtils.GetGroupedObjects(tr, hole);
                                        // 如果组里面只有一个，直接删除组
                                        if (objectIds.Count() == 1)
                                        {
                                            group.Erase();
                                        }
                                        // 如果组里面有两个，并且两个编码一样
                                        if (objectIds.Count() == 2 && objectIds[0] == objectIds[1])
                                        {
                                            group.Erase();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    tr.Commit();
                }
            }

            static bool IsPolygonHasHole(Transaction tr, Entity entity)
            {
                ObjectId[] entityIds = GroupUtils.GetGroupedObjects(tr, entity);
                foreach (ObjectId entityId in entityIds)
                {
                    // 如果是本地块，直接skip
                    if (entity.ObjectId == entityId)
                        continue;

                    using (var ent = tr.GetObject(entityId, OpenMode.ForRead))
                    {
                        if (ent is Polyline2d || ent is Polyline)
                        {
                            string cassFlag = CadUtils.GetCassFlag(ent);
                            if (CassFlagIsland.ToLower() == cassFlag.ToLower())
                                return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
