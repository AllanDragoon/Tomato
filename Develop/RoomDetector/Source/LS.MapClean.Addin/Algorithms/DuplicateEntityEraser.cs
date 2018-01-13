using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Security.Policy;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.Algorithms
{
    public class DuplicateEntityEraserBsp : AlgorithmWithEditor
    {
        private double _tolerance = 0.01;

        private IEnumerable<CurveCrossingInfo> _crossingInfos;
        public IEnumerable<CurveCrossingInfo> CrossingInfos
        {
            get { return _crossingInfos; }
        }

        public DuplicateEntityEraserBsp(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();
            if (!selectedObjectIds.Any())
                return;

            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var curve2dBspBuilder = new Curve2dBspBuilder(selectedObjectIds, transaction);
                _crossingInfos = curve2dBspBuilder.SearchDuplicateEntities();

                transaction.Commit();
            }

            //int count = 0;
            //foreach (var curveCrossingInfo in _crossingInfos)
            //{
            //    count += curveCrossingInfo.IntersectPoints.Length;
            //}
            //Editor.WriteMessage("\n共{0}个交点", count);

            // the code that you want to measure comes here
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n查找重复实体花费时间{0}毫秒", elapsedMs);
        }
    }

    public class DuplicateEntityEraserKdTree : AlgorithmWithEditor
    {
        private double _tolerance = 0.2;

        private IEnumerable<CurveCrossingInfo> _crossingInfos;
        public IEnumerable<CurveCrossingInfo> CrossingInfos
        {
            get { return _crossingInfos; }
        }

        public DuplicateEntityEraserKdTree(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (selectedObjectIds == null || !selectedObjectIds.Any())
                return;
            var watch = Stopwatch.StartNew();

            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                //// Build a kd tree from all curve vertices.
                Dictionary<ObjectId, CurveVertex[]> curveVertices = null;
                CurveVertexKdTree<CurveVertex> kdTree = null;
                BuildCurveVertexKdTree(selectedObjectIds, transaction, out curveVertices, out kdTree);
                SearchDuplicateEntities(curveVertices, kdTree);
                //var kdTree = BuildCurveVertexKdTree2(selectedObjectIds, transaction);
                //SearchDuplicateEntities2(selectedObjectIds, kdTree, transaction);
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            //Editor.WriteMessage("\n查找重复实体花费时间{0}毫秒", elapsedMs);
        }

        private CurveVertexKdTree<CurveVertex> BuildCurveVertexKdTree2(IEnumerable<ObjectId> allIds, Transaction transaction)
        {
            var allVertices = new List<CurveVertex>();
            foreach (var id in allIds)
            {
                var points = CurveUtils.GetDistinctVertices(id, transaction);
                if (!points.Any())
                    continue;

                var vertices = points.Select(it => new CurveVertex(it, id));

                allVertices.AddRange(vertices);
            }

            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);
            return kdTree;
        }

        private void SearchDuplicateEntities2(IEnumerable<ObjectId> curveIds, CurveVertexKdTree<CurveVertex> kdTree, Transaction transaction)
        {
            var crossingInfos = new List<CurveCrossingInfo>();
            var visited = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            foreach (var id in curveIds)
            {
                var infos = SearchDuplicateEntities2(id, kdTree, visited, transaction);
                if (infos.Any())
                {
                    crossingInfos.AddRange(infos);
                }
            }
            _crossingInfos = crossingInfos;
        }

        private IEnumerable<CurveCrossingInfo> SearchDuplicateEntities2(ObjectId curveId, CurveVertexKdTree<CurveVertex> kdTree, 
            HashSet<KeyValuePair<ObjectId, ObjectId>> visited, Transaction transaction)
        {
            var result = new List<CurveCrossingInfo>();
            var curve = transaction.GetObject(curveId, OpenMode.ForRead) as Curve;
            if(curve == null)
                return new List<CurveCrossingInfo>();
            var extents = curve.GeometricExtents;
            var nearVertices = kdTree.BoxedRange(extents.MinPoint.ToArray(), extents.MaxPoint.ToArray());
            foreach (var nearVertex in nearVertices)
            {
                if (nearVertex.Id == curveId ||
                    visited.Contains(new KeyValuePair<ObjectId, ObjectId>(curveId, nearVertex.Id)) ||
                    visited.Contains(new KeyValuePair<ObjectId, ObjectId>(nearVertex.Id, curveId)))
                    continue;
                var sourceVertices = CurveUtils.GetDistinctVertices(curve, transaction);
                var targetVertices = CurveUtils.GetDistinctVertices(nearVertex.Id, transaction);
                var identical = PolygonIncludeSearcher.AreIdenticalCoordinates(sourceVertices, targetVertices);
                if(identical)
                    result.Add(new CurveCrossingInfo(curveId, nearVertex.Id, sourceVertices.ToArray()));
                visited.Add(new KeyValuePair<ObjectId, ObjectId>(curveId, nearVertex.Id));

            }
            return result;
        }


        private void BuildCurveVertexKdTree(IEnumerable<ObjectId> allIds, Transaction transaction,
            out Dictionary<ObjectId, CurveVertex[]> curveVertices, out CurveVertexKdTree<CurveVertex> kdTree)
        {
            curveVertices = new Dictionary<ObjectId, CurveVertex[]>();
            var allVertices = new List<CurveVertex>();
            foreach (var id in allIds)
            {
                var points = CurveUtils.GetDistinctVertices(id, transaction);
                if (!points.Any())
                    continue;

                var vertices = points.Select(it => new CurveVertex(it, id));
                curveVertices[id] = vertices.ToArray();

                allVertices.AddRange(vertices);
            }

            kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it=>it.Point.ToArray(), ignoreZ: true);
        }

        private void SearchDuplicateEntities(Dictionary<ObjectId, CurveVertex[]> curveInfos, CurveVertexKdTree<CurveVertex> kdTree)
        {
            var crossingInfos = new List<CurveCrossingInfo>();
            List<Tuple<ObjectId, ObjectId>> visited = new List<Tuple<ObjectId, ObjectId>>();

            foreach (var curveInfo in curveInfos)
            {
                var infos = SearchDuplicateInfo(curveInfo, kdTree, curveInfos, visited);
                if (infos.Any())
                { 
                    crossingInfos.AddRange(infos);
                }
            }
            _crossingInfos = crossingInfos;
        }

        private IEnumerable<CurveCrossingInfo> SearchDuplicateInfo(KeyValuePair<ObjectId, CurveVertex[]> curveInfo,
            CurveVertexKdTree<CurveVertex> kdTree, Dictionary<ObjectId, CurveVertex[]> curveInfos, List<Tuple<ObjectId, ObjectId>> visited)
        {
            List<CurveCrossingInfo> crossingInfos = new List<CurveCrossingInfo>();
            HashSet<ObjectId> candidates = null;
            Dictionary<ObjectId, List<Point3d>> candidatePoints = new Dictionary<ObjectId, List<Point3d>>();

            foreach (var vertex in curveInfo.Value)
            {
                var neighbours = kdTree.NearestNeighbours(vertex.Point.ToArray(), _tolerance);
                neighbours = neighbours.Except(new CurveVertex[] {vertex});

                // If there is no neighbour vertex, just return.
                if (!neighbours.Any())
                {
                    if (candidates != null)
                        candidates = null;
                    break;
                }

                if (candidates == null)
                {
                    candidates = new HashSet<ObjectId>(neighbours.Select(it => it.Id));
                    foreach (var id in candidates)
                    {
                        var points = neighbours.Where(it => it.Id == id)
                            .Select(it => it.Point);
                        candidatePoints[id] = new List<Point3d>(points);
                    }
                }
                else
                {
                    candidates.IntersectWith(neighbours.Select(it => it.Id));
                    if (candidates.Count <= 0)
                        break;

                    foreach (var id in candidates)
                    {
                        var points = candidatePoints[id];
                        var newPoints = neighbours
                            .Where(it => it.Id == id)
                            .Select(it => it.Point);
                        foreach (var newPoint in newPoints)
                        {
                            if (!points.Contains(newPoint))
                                points.Add(newPoint);
                        }
                    }
                }
            }

            if (candidates != null && candidates.Count > 0)
            {
                foreach (var candidate in candidates)
                {
                    // If self, just continue.
                    if (candidate == curveInfo.Key)
                        continue;

                    var existing = visited.FirstOrDefault(it =>
                        (it.Item1 == curveInfo.Key && it.Item2 == candidate) ||
                        (it.Item1 == candidate && it.Item2 == curveInfo.Key));

                    if (existing != null)
                        continue;

                    visited.Add(new Tuple<ObjectId, ObjectId>(curveInfo.Key, candidate));

                    // Check whether they have same number of vertices.
                    if (curveInfos[candidate].Length != candidatePoints[candidate].Count)
                        continue;

                    var points = curveInfo.Value.Select(it => it.Point).ToArray();
                    var crossingInfo = new CurveCrossingInfo(curveInfo.Key, candidate, points);
                    crossingInfos.Add(crossingInfo);
                }
            }
            return crossingInfos;
        }
    }

    /// <summary>
    /// 
    /// Note: copy most of the code logic from \LS.FishingBait\Source\ArxUtils\Topology\DuplicateVertex.cs
    /// NB:DuplicateCurves is not same as DuplicateEntities in LS.FishingBait
    /// </summary>
    public class DuplicateEntityEraser : AlgorithmWithEditor
    {
        private double _tolerance = 0.1;
        // TODO: Will be updated in future.
        //private List<string> _layers = new List<string>() { "0" };

        public DuplicateEntityEraser(Editor editor, double tolerance)
            : base(editor)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var layers = new List<String> { "0" };
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            Check(true, true);
        }

        public void Fix()
        {
            Fix(true, true);
        }

        private ObjectIdCollection _duplicateVertexCurveIds;
        private IList<ObjectIdCollection> _duplicateCurveIds;

        public ObjectIdCollection DuplicateVertexCurveIds
        {
            get { return _duplicateVertexCurveIds; }
        }

        public IList<ObjectIdCollection> DuplicateCurveIds
        {
            get { return _duplicateCurveIds; }
        }

        public void Check(bool checkVertex, bool checkCurve)
        {
            if (checkVertex)
            {
                _duplicateVertexCurveIds = GetDuplicateVertexCurveIds();
            }

            if (checkCurve)
            {
                _duplicateCurveIds = GetDuplicateCurveIds();
            }
        }

        public void Fix(bool fixVertex, bool fixCurve)
        {
            var database = base.Editor.Document.Database;
            // Fix duplicate vertex
            if (fixVertex)
            {
                using (Transaction trans = database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId duplicateVertexCurveId in _duplicateVertexCurveIds)
                    {
                        FixDuplicateVertexFromPolyline(duplicateVertexCurveId, _tolerance);
                    }
                    trans.Commit();
                }
            }

            // Fix duplicate curve
            if (fixCurve)
            {
                FixDuplicateCurves();
            }
        }

        public static bool HasDuplicateVertex(ObjectId objectId, double tolerance)
        {
            using (var tr = objectId.Database.TransactionManager.StartTransaction())
            {
                var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                if (curve != null && HasDuplicateVertex(tr, curve, tolerance))
                    return true;
                tr.Commit();
            }
            return false;
        }

        public static bool HasDuplicateVertex(Transaction tr, Curve curve, double tolerance)
        {
            var polyline = curve as Polyline;
            if (polyline != null)
            {
                var count = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
                for (int i = 0; i < count; i++)
                {
                    var segmentType = polyline.GetSegmentType(i);
                    double distance = 0.0;
                    if (segmentType == SegmentType.Arc)
                    {
                        var arc = polyline.GetArcSegmentAt(i);
                        distance = arc.GetLength(arc.GetParameterOf(arc.StartPoint), arc.GetParameterOf(arc.EndPoint),
                            tolerance);
                    }
                    else if (segmentType == SegmentType.Line)
                    {
                        var line = polyline.GetLineSegmentAt(i);
                        distance = line.GetLength(line.GetParameterOf(line.StartPoint),
                            line.GetParameterOf(line.EndPoint),
                            tolerance);
                    }
                    //else if(segmentType == SegmentType.Point && i == polyline.NumberOfVertices - 1)
                    //    return;

                    if (double.IsNaN(distance) || distance < tolerance)
                        return true;
                }
            }

            var polyline2d = curve as Polyline2d;
            if (polyline2d != null)
            {
                var vertexPositionQueue = new List<Vertex2d>();
                foreach (ObjectId vId in polyline2d)
                {
                    if (vId.IsErased)
                        continue;
                    var v2d = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                    if (v2d == null)
                        continue;
                    vertexPositionQueue.Add(v2d);
                }
                for (int i = 0; i < vertexPositionQueue.Count; i++)
                {
                    double distance = 0.0;
                    if (i < vertexPositionQueue.Count - 1)
                    {
                        distance = (vertexPositionQueue[i].Position - vertexPositionQueue[i + 1].Position).Length;
                    }
                    else
                    {
                        distance = (vertexPositionQueue[i].Position - vertexPositionQueue[0].Position).Length;
                    }

                    if (double.IsNaN(distance) || distance < tolerance)
                        return true;
                }
            }
            return false;
        }

        public static void FixDuplicateVertexFromPolyline(ObjectId objectId, double tolerance)
        {
            using (Application.DocumentManager.MdiActiveDocument.LockDocument(DocumentLockMode.ProtectedAutoWrite, null, null, true))
            {
                using (var tr = objectId.Database.TransactionManager.StartTransaction())
                {
                    var curve = tr.GetObject(objectId, OpenMode.ForWrite) as Curve;
                    if (curve != null)
                        FixDuplicateVertexFromPolyline(tr, curve, tolerance);
                    tr.Commit();
                }
            }
        }

        public static void FixDuplicateVertexFromPolyline(Transaction tr, Curve curve, double tolerance)
        {
            var polyline = curve as Polyline;
            if (polyline != null)
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    var segmentType = polyline.GetSegmentType(i);
                    double distance = 0.0;
                    if (segmentType == SegmentType.Arc)
                    {
                        var arc = polyline.GetArcSegmentAt(i);
                        distance = arc.GetLength(arc.GetParameterOf(arc.StartPoint), arc.GetParameterOf(arc.EndPoint),
                            tolerance);
                    }
                    else if (segmentType == SegmentType.Line)
                    {
                        var line = polyline.GetLineSegmentAt(i);
                        distance = line.GetLength(line.GetParameterOf(line.StartPoint),
                            line.GetParameterOf(line.EndPoint),
                            tolerance);
                    }
                    //else if(segmentType == SegmentType.Point && i == polyline.NumberOfVertices - 1)
                    //    return;

                    if ((double.IsNaN(distance) || distance < tolerance))
                    {
                        if (i < polyline.NumberOfVertices - 1)
                        {
                            double bulge = polyline.GetBulgeAt(i + 1);
                            polyline.RemoveVertexAt(i + 1);
                            polyline.SetBulgeAt(i, bulge);
                            i--;
                        }
                        else if (i == polyline.NumberOfVertices - 1)
                        {
                            if (!polyline.Closed)
                                polyline.Closed = true;

                            double lastBulge = polyline.GetBulgeAt(polyline.NumberOfVertices - 2);
                            polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
                            polyline.SetBulgeAt(polyline.NumberOfVertices - 1, lastBulge);
                        }
                    }
                }
            }

            var polyline2d = curve as Polyline2d;
            if (polyline2d != null)
            {
                var vertexPositionQueue = new List<Vertex2d>();
                foreach (ObjectId vId in polyline2d)
                {
                    if (vId.IsErased)
                        continue;
                    var v2d = tr.GetObject(vId, OpenMode.ForWrite) as Vertex2d;
                    if (v2d == null)
                        continue;
                    vertexPositionQueue.Add(v2d);
                }
                for (int i = 0; i < vertexPositionQueue.Count; i++)
                {
                    double distance = 0.0;
                    if (i < vertexPositionQueue.Count - 1)
                    {
                        distance = (vertexPositionQueue[i].Position - vertexPositionQueue[i + 1].Position).Length;
                    }
                    else
                    {
                        distance = (vertexPositionQueue[i].Position - vertexPositionQueue[0].Position).Length;
                    }

                    if (double.IsNaN(distance) || distance < tolerance)
                    {
                        if (i < vertexPositionQueue.Count - 1)
                        {
                            vertexPositionQueue[i + 1].Erase();
                            vertexPositionQueue.RemoveAt(i + 1);
                            i--;
                        }
                        else if (i == vertexPositionQueue.Count - 1)
                        {
                            if (!polyline2d.Closed)
                            {
                                polyline2d.Closed = true;
                            }

                            vertexPositionQueue[i].Erase();
                            vertexPositionQueue.RemoveAt(i);
                        }
                    }
                }
            }
        }

        public ObjectIdCollection GetDuplicateVertexCurveIds()
        {
            var duplicateVertexCurveIds = new ObjectIdCollection();
            var database = base.Editor.Document.Database;
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var allCurves = new List<Curve>();

                foreach (var objectId in modelSpace)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all specified layers curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        //// Check if the curve is from specified layers.
                        //if (_layers.Contains(curve.Layer) == false)
                        //    continue;
                        allCurves.Add(curve);
                    }
                }

                // Check Duplicate vertexs.
                foreach (Curve curve in allCurves)
                {
                    if (HasDuplicateVertex(trans, curve, _tolerance))
                    {
                        duplicateVertexCurveIds.Add(curve.ObjectId);

                    }
                }

                trans.Abort();
            }

            return duplicateVertexCurveIds;
        }

        public void FixDuplicateCurves()
        {
            var database = base.Editor.Document.Database;
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectIdCollection duplicateCurveIdCollection in _duplicateCurveIds)
                {
                    // 每组只留下第一个，剩下都干掉
                    for (int i = 1; i < duplicateCurveIdCollection.Count; i++)
                    {
                        var obj = trans.GetObject(duplicateCurveIdCollection[i], OpenMode.ForWrite);
                        obj.Erase();
                    }
                }
                trans.Commit();
            }
        }

        // DuplicateCurves会找到起始点重复的直线，多段线，圆弧
        public IList<ObjectIdCollection> GetDuplicateCurveIds()
        {
            var duplicateCurveIdCollections = new List<ObjectIdCollection>();
            var database = base.Editor.Document.Database;
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var allCurves = new List<Curve>();

                foreach (var objectId in modelSpace)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all specified layers curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        //// Check if the curve is from specified layers.
                        //if (_layers.Contains(curve.Layer) == false)
                        //    continue;
                        allCurves.Add(curve);
                    }
                }

                // Temp duplicate curve collections which is used for determining if the the specified curve need to check it's duplicate curves.
                var allDuplicateCurveIds = new ObjectIdCollection();

                // Check Duplicate curves.
                foreach (Curve curve in allCurves)
                {
                    // Filter out the curve if it has alread in duplicateCurveIds collection.
                    if (allDuplicateCurveIds.Contains(curve.ObjectId))
                        continue;

                    var duplicateCurveIds = GetDuplicateCurveIdsByCurve(trans, curve);
                    if (duplicateCurveIds.Count > 0)
                    {
                        duplicateCurveIdCollections.Add(duplicateCurveIds);
                        foreach (ObjectId duplicateCurveId in duplicateCurveIds)
                        {
                            allDuplicateCurveIds.Add(duplicateCurveId);
                        }
                    }
                }

                trans.Abort();
            }

            return duplicateCurveIdCollections;
        }

        public ObjectIdCollection GetDuplicateCurveIdsByCurve(Transaction trans, Curve curve)
        {
            var duplicateCurveIds = new ObjectIdCollection();
            var entityIds = EditorUtils.SelectObjectsByEntityBoundingBox(base.Editor, SelectionFilterUtils.OnlySelectCurve(), curve, _tolerance * 2);

            var currentLine = curve as Line;
            var currentArc = curve as Arc;
            var currentPline = curve as Polyline;
            var currentPline2d = curve as Polyline2d;

            foreach (ObjectId entityId in entityIds)
            {
                // Filter out itself.
                if (entityId == curve.ObjectId)
                    continue;

                var entity = trans.GetObject(entityId, OpenMode.ForRead) as Curve;
                if (entity == null)
                    continue;

                // 所有的比较，都需要比较起始点和终点是否重合，如果不重合，则认为不是duplicate curves.
                if (IsStartAndEndPointsEquals(curve, entity) == false)
                    continue;

                var line = entity as Line;
                var arc = entity as Arc;
                var pline = entity as Polyline;
                var pline2d = entity as Polyline2d;

                bool isDuplicate = false;

                if (currentLine != null)
                {
                    // 找Line的重复直线或多段线
                    // 如果是Line->Line,则为重合
                    // 如果是Line->Polyline,则比较长度
                    // 如果是Line->Polyline2d,则比较长度
                    if (line != null)
                    {
                        isDuplicate = true;
                    }
                    else if (pline != null && IsLengthEquals(currentLine, pline, _tolerance))
                    {
                        isDuplicate = true;
                    }
                    else if (pline2d != null && IsLengthEquals(currentLine, pline2d, _tolerance))
                    {
                        isDuplicate = true;
                    }
                }
                else if (currentArc != null)
                {
                    // 找Arc的重复Arc,比较Arc的StartAngle和EndAngle
                    if (arc != null)
                    {
                        if ((((currentArc.StartPoint - arc.StartPoint).Length < _tolerance) &&
                             Math.Abs(currentArc.StartAngle - arc.StartAngle) < _tolerance &&
                             Math.Abs(currentArc.EndAngle - arc.EndAngle) < _tolerance) ||
                            (((currentArc.StartPoint - arc.EndPoint).Length < _tolerance) &&
                             Math.Abs(currentArc.StartAngle - arc.EndAngle) < _tolerance &&
                             Math.Abs(currentArc.EndAngle - arc.StartAngle) < _tolerance))
                        {
                            isDuplicate = true;
                        }
                    }
                }
                else if (currentPline != null)
                {
                    // 找Polyline的重复直线或多段线
                    // 如果是Polyline->Line,则比较及长度
                    // 如果是Polyline->Polyline 则比较所有节点位置
                    // 如果是Polyline->Polyline2d 则比较所有节点位置
                    if (line != null && IsLengthEquals(line, currentPline, _tolerance))
                    {
                        isDuplicate = true;
                    }
                    else if (pline != null && IsVertexesEquals(currentPline, pline, _tolerance, trans))
                    {
                        isDuplicate = true;
                    }
                    else if (pline2d != null && IsVertexesEquals(currentPline, pline2d, _tolerance, trans))
                    {
                        isDuplicate = true;
                    }
                }
                else if (currentPline2d != null)
                {
                    // 找Polyline的重复直线或多段线
                    // 如果是Polyline2d->Line,则比较长度
                    // 如果是Polyline->Polyline 则比较所有节点位置
                    // 如果是Polyline->Polyline2d 则比较所有节点位置
                    if (line != null && IsLengthEquals(line, currentPline2d, _tolerance))
                    {
                        isDuplicate = true;
                    }
                    else if (pline != null && IsVertexesEquals(pline, currentPline2d, _tolerance, trans))
                    {
                        isDuplicate = true;
                    }
                    else if (pline2d != null && IsVertexesEquals(currentPline2d, pline2d, _tolerance, trans))
                    {
                        isDuplicate = true;
                    }
                }
                if (isDuplicate)
                    duplicateCurveIds.Add(entityId);
            }

            // 如果找到重复线，则将其自己也加入到duplicateCurveIds
            if (duplicateCurveIds.Count > 0)
                duplicateCurveIds.Add(curve.ObjectId);

            return duplicateCurveIds;
        }

        // 判断两条Curve的start和end是否重合
        private bool IsStartAndEndPointsEquals(Curve curve1, Curve curve2)
        {
            return ((curve1.StartPoint - curve2.StartPoint).Length < _tolerance &&
                    (curve1.EndPoint - curve2.EndPoint).Length < _tolerance) ||
                   ((curve1.StartPoint - curve2.EndPoint).Length < _tolerance &&
                    (curve1.EndPoint - curve2.StartPoint).Length < _tolerance);
        }

        private static bool IsLengthEquals(Line line, Polyline polyline, double tolerance)
        {
            return Math.Abs(line.Length - polyline.Length) < tolerance;
        }

        private static bool IsLengthEquals(Line line, Polyline2d polyline2d, double tolerance)
        {
            return Math.Abs(line.Length - polyline2d.Length) < tolerance;
        }

        private static bool IsVertexesEquals(Polyline polyline1, Polyline polyline2, double tolerance, Transaction transaction)
        {
            if (polyline1.NumberOfVertices != polyline2.NumberOfVertices)
                return false;

            return IsPointsEquals(GetPoint3dCollection(polyline1, transaction), GetPoint3dCollection(polyline2, transaction), tolerance);
        }

        private static bool IsVertexesEquals(Polyline polyline, Polyline2d polyline2d, double tolerance, Transaction transaction)
        {
            var vertexesInPolyline2d = GetPoint3dCollection(polyline2d, transaction);
            if (polyline.NumberOfVertices != vertexesInPolyline2d.Count)
                return false;

            return IsPointsEquals(GetPoint3dCollection(polyline, transaction), vertexesInPolyline2d, tolerance);
        }

        private static bool IsVertexesEquals(Polyline2d polyline2d1, Polyline2d polyline2d2, double tolerance, Transaction transaction)
        {
            var vertexesInPolyline2d1 = GetPoint3dCollection(polyline2d1, transaction);
            var vertexesInPolyline2d2 = GetPoint3dCollection(polyline2d2, transaction);
            if (vertexesInPolyline2d1.Count != vertexesInPolyline2d2.Count)
                return false;

            return IsPointsEquals(vertexesInPolyline2d1, vertexesInPolyline2d2, tolerance);
        }

        private static bool IsPointsEquals(Point3dCollection point3DCollection1, Point3dCollection point3DCollection2, double tolerance)
        {
            bool hasDifferentPoints = false;
            foreach (Point3d point in point3DCollection1)
            {
                bool isDiffernt = true;
                foreach (Point3d point3d in point3DCollection2)
                {
                    if ((point - point3d).Length < tolerance)
                    {
                        isDiffernt = false;
                        break;
                    }
                }
                if (isDiffernt)
                {
                    hasDifferentPoints = true;
                    break;
                }
            }
            return !hasDifferentPoints;
        }

        // Only works for Polyline and Polyline2d
        private static Point3dCollection GetPoint3dCollection(Curve curve, Transaction transaction)
        {
            var points = new Point3dCollection();
            var polyline = curve as Polyline;
            if (polyline != null)
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    points.Add(polyline.GetPoint3dAt(i));
                }
            }
            else
            {
                var polyline2d = curve as Polyline2d;
                if (polyline2d != null)
                {
                    foreach (ObjectId vertexId in polyline2d)
                    {
                        var vertex2d = transaction.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                        if (vertex2d == null)
                            continue;
                        points.Add(new Point3d(vertex2d.Position.X, vertex2d.Position.Y, 0));
                    }
                }
            }

            return points;
        }

        public class SortLinesByLength : IComparer<Line>
        {
            public int Compare(Line a, Line b)
            {
                double value = a.Length - b.Length;
                if (value.EqualsWithTolerance(0.0))
                    return 0;
                else if (value.Larger(0))
                    return 1;
                else
                    return -1;
            }
        }
    }
}
