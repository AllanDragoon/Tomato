using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public struct CurveCrossingPoints
    {
        public ObjectId curveId;
        public Point3dCollection points;
    }

    public class BreakCrossingObjectsBsp : AlgorithmWithEditor
    {
        private double _tolerance = 0.01;

        private IEnumerable<CurveCrossingInfo> _crossingInfos;
        public IEnumerable<CurveCrossingInfo> CrossingInfos
        {
            get { return _crossingInfos; }
        }

        public BreakCrossingObjectsBsp(Editor editor, double tolerance)
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
                IEnumerable<CurveCrossingInfo> duplicateEntities = null;
                _crossingInfos = curve2dBspBuilder.SearchRealIntersections(true, out duplicateEntities);

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
            //Editor.WriteMessage("\n查找交叉对象花费时间{0}毫秒", elapsedMs);
        }

        public Dictionary<ObjectId, IEnumerable<ObjectId>> Fix(bool eraseOld)
        {
            var result = new Dictionary<ObjectId, IEnumerable<ObjectId>>();
            if (_crossingInfos == null || !_crossingInfos.Any())
                return result;

            // Group intersect points by ObjectId.
            Dictionary<ObjectId, List<Point3d>> intersects = null;
            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
            GroupIntersectPoints(out intersects, out selfIntersects);

            var document = Editor.Document;
            using (var docLock = document.LockDocument())
            {
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
                    foreach (var pair in intersects)
                    {
                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, false, eraseOld);
                        result.Add(pair.Key, ids);
                    }

                    foreach (var pair in selfIntersects)
                    {
                        var ids = SplitCurve(pair.Key, pair.Value, modelSpace, transaction, true, eraseOld);
                        result.Add(pair.Key, ids);
                    }

                    transaction.Commit();
                }
            }
            return result;
        }

        public IEnumerable<Entity> DummyFix()
        {
            if (_crossingInfos == null || !_crossingInfos.Any())
                return new Entity[0];

            // Group intersect points by ObjectId.
            Dictionary<ObjectId, List<Point3d>> intersects = null;
            Dictionary<ObjectId, List<Point3d>> selfIntersects = null;
            GroupIntersectPoints(out intersects, out selfIntersects);

            var result = new List<Entity>();
            var document = Editor.Document;
            using (var docLock = document.LockDocument())
            {
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForWrite);
                    foreach (var pair in intersects)
                    {
                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, false);
                        foreach (Entity ent in ents)
                        {
                            result.Add(ent);
                        }
                    }

                    foreach (var pair in selfIntersects)
                    {
                        var ents = DummySplitCurve(pair.Key, pair.Value, modelSpace, transaction, true);
                        foreach (Entity ent in ents)
                        {
                            result.Add(ent);
                        }
                    }

                    transaction.Commit();
                }
            }
            return result;
        }

        private IEnumerable<ObjectId> SplitCurve(ObjectId objId, IEnumerable<Point3d> points, BlockTableRecord modelSpace,
            Transaction transaction, bool selfIntersect, bool eraseOld)
        {
            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
            if (curve == null)
                return new ObjectId[0];

            var result = new List<ObjectId>();
            DBObjectCollection spliltCurves = null;
            if (selfIntersect)
            {
                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
            }
            else
            {
                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
            }

            // The splitted curves has the same layer with original curve, 
            // so we needn't set its layer explicitly.
            foreach (Curve splitCurve in spliltCurves)
            {
                var id  = modelSpace.AppendEntity(splitCurve);
                transaction.AddNewlyCreatedDBObject(splitCurve, true);
                result.Add(id);
            }

            if (spliltCurves.Count > 0)
            {
                if (eraseOld)
                {
                    // Erase the old one
                    curve.Erase();
                }
            }
            else
            {
                result.Add(curve.Id);
            }
            return result;
        }

        private DBObjectCollection DummySplitCurve(ObjectId objId, IEnumerable<Point3d> points, 
            BlockTableRecord modelSpace, Transaction transaction, bool selfIntersect)
        {
            var curve = transaction.GetObject(objId, OpenMode.ForWrite) as Curve;
            if (curve == null)
                return new DBObjectCollection();

            var result = new List<ObjectId>();
            DBObjectCollection spliltCurves = null;
            if (selfIntersect)
            {
                spliltCurves = CurveUtils.SplitSelfIntersectCurve(curve, points.ToArray(), transaction);
            }
            else
            {
                spliltCurves = CurveUtils.SplitCurve(curve, points.ToArray());
            }
            return spliltCurves;
        }

        private void GroupIntersectPoints(out Dictionary<ObjectId, List<Point3d>> intersects, 
            out Dictionary<ObjectId, List<Point3d>> selfIntersects)
        {
            intersects = new Dictionary<ObjectId, List<Point3d>>();
            selfIntersects = new Dictionary<ObjectId, List<Point3d>>();

            foreach (var crossingInfo in _crossingInfos)
            {
                // Self intersection
                if (crossingInfo.SourceId == crossingInfo.TargetId)
                {
                    // Move points from intersects to selfIntersects.
                    List<Point3d> points = null;
                    if (intersects.TryGetValue(crossingInfo.SourceId, out points))
                    {
                        intersects.Remove(crossingInfo.SourceId);

                        if (selfIntersects.ContainsKey(crossingInfo.SourceId))
                        {
                            var targetPoints = selfIntersects[crossingInfo.SourceId];
                            foreach (var point in points)
                            {
                                if (!targetPoints.Contains(point))
                                    targetPoints.Add(point);
                            }
                        }
                        else
                        {
                            selfIntersects[crossingInfo.SourceId] = points;
                        }

                        // Add new ones.
                        var selfIntersectPoints = selfIntersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!selfIntersectPoints.Contains(point))
                                selfIntersectPoints.Add(point);
                        }
                    }
                    else // Add it to selfIntersects.
                    {
                        selfIntersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                }
                else // Non-self-intersection
                {
                    if (selfIntersects.ContainsKey(crossingInfo.SourceId))
                    {
                        var points = selfIntersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                    else if (!intersects.ContainsKey(crossingInfo.SourceId))
                    {
                        intersects[crossingInfo.SourceId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                    else
                    {
                        var points = intersects[crossingInfo.SourceId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }

                    if (selfIntersects.ContainsKey(crossingInfo.TargetId))
                    {
                        var points = selfIntersects[crossingInfo.TargetId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                    else if (!intersects.ContainsKey(crossingInfo.TargetId))
                    {
                        intersects[crossingInfo.TargetId] = new List<Point3d>(crossingInfo.IntersectPoints);
                    }
                    else
                    {
                        var points = intersects[crossingInfo.TargetId];
                        foreach (var point in crossingInfo.IntersectPoints)
                        {
                            if (!points.Contains(point))
                                points.Add(point);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 
    /// Note : Similar to AutoSplitCurves in FishingBait. But there are some differences in logic.
    /// </summary>
    public class BreakCrossingObjects : AlgorithmWithEditor
    {
        private double _tolerance = 0.01;
        // TODO: Will be updated in future.
        //private List<string> _layers = new List<string>(){"0"}; 

        public BreakCrossingObjects(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// Check results
        /// </summary>
        public IEnumerable<CurveCrossingPoints> CrossingPoints { get; private set; }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();
            CrossingPoints = GetCrossingPointsesForCurves();

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n查找交叉对象花费时间{0}毫秒", elapsedMs);
        }

        public void Fix()
        {
            if (CrossingPoints == null || !CrossingPoints.Any())
                return;

            var database = base.Editor.Document.Database;
            // 通过每条线上的交叉点来分割线
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (var curveCrossingPoints in CrossingPoints)
                {
                    var curve = trans.GetObject(curveCrossingPoints.curveId, OpenMode.ForWrite) as Curve;
                    if (curve != null)
                    {
                        var sortedPoints = SortBreakPointsByCurve(curve, curveCrossingPoints.points);
                        var splitedCurves = curve.GetSplitCurves(sortedPoints);
                        // 将分割出来的curves添加到model space.
                        foreach (DBObject splitedCurveObj in splitedCurves)
                        {
                            var splitedCurve = splitedCurveObj as Entity;
                            if(splitedCurve == null)
                                continue;
                            modelSpace.AppendEntity(splitedCurve);
                            trans.AddNewlyCreatedDBObject(splitedCurve, true);
                        }
                        // 删除原始线
                        curve.Erase();
                    }
                }
                trans.Commit();
            }
        }

        private static Point3dCollection SortBreakPointsByCurve(Curve curve, Point3dCollection points)
        {
            var sortedPoints = new Point3dCollection();
            if (curve != null)
            {
                foreach (Point3d point in points)
                {
                    if (sortedPoints.Count == 0)
                    {
                        sortedPoints.Add(point);
                        continue;
                    }
                    var parameter = GeometryUtils.GetPointParameter(curve, point);
                    bool inserted = false;
                    for (int i = 0; i < sortedPoints.Count; i++)
                    {
                        if (GeometryUtils.GetPointParameter(curve, sortedPoints[i]) > parameter)
                        {
                            sortedPoints.Insert(i, point);
                            inserted = true;
                            break;
                        }
                    }

                    // If none of the points in sortedPoints is not larger than point, add point to the tail.
                    if(inserted == false)
                        sortedPoints.Add(point);

                }
            }

            return sortedPoints;
        }


        private IEnumerable<CurveCrossingPoints> GetCrossingPointsesForCurves()
        {
            var curveCrossingPointsList = new List<CurveCrossingPoints>();
            var database = base.Editor.Document.Database;

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var allCurveIds = new ObjectIdCollection();

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
                        allCurveIds.Add(objectId);
                    }
                }

                // 遍历所有的objects，对每个object查找其所在bounding box里相交的点。
                foreach (ObjectId curveId in allCurveIds)
                {
                    var currentCurve = trans.GetObject(curveId, OpenMode.ForRead) as Curve;
                    if (currentCurve == null)
                        continue;

                    var allIntersectPoints = new Point3dCollection();
                    var boundingBoxEntityIds = EditorUtils.SelectObjectsByEntityBoundingBox(base.Editor, SelectionFilterUtils.OnlySelectCurve(), currentCurve, _tolerance * 2);

                    var points = new Point3dCollection();
                    foreach (ObjectId boundingBoxEntityId in boundingBoxEntityIds)
                    {
                        var curve = trans.GetObject(boundingBoxEntityId, OpenMode.ForRead) as Curve;
                        if (curve == null || (curve.ObjectId != ObjectId.Null && curve.ObjectId == currentCurve.ObjectId))
                            continue;

                        currentCurve.IntersectWith(curve, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);

                        foreach (Point3d point in points)
                        {
                            allIntersectPoints.Add(point);

                            //// For testing Intersect point.
                            //DBPoint dbPoint = new DBPoint(point);
                            //modelSpace.AppendEntity(dbPoint);
                            //trans.AddNewlyCreatedDBObject(dbPoint, true);
                        }
                    }

                    var crossingPoints = GetCurveIntersectionPoints(currentCurve, allIntersectPoints);
                    if (crossingPoints.Count > 0)
                    {
                        var curveCrossingPoints = new CurveCrossingPoints
                        {
                            curveId = currentCurve.ObjectId,
                            points = crossingPoints
                        };
                        curveCrossingPointsList.Add(curveCrossingPoints);
                    }
                }

                trans.Abort();
            }

            return curveCrossingPointsList;
        }

        private static Point3dCollection GetCurveIntersectionPoints(DBObject dbObject, Point3dCollection potentailPoints)
        {
            var splitPoints = new Point3dCollection();

            var curve = dbObject as Curve;
            if (curve == null)
                return splitPoints;

            foreach (Point3d point in potentailPoints)
            {
                // Filter out the point which is at curve's start or end potentailPoints.
                if ((point - curve.StartPoint).Length > Tolerance.Global.EqualPoint &&
                    (point - curve.EndPoint).Length > Tolerance.Global.EqualPoint)
                {
                    bool isPointOnCurve = GeometryUtils.IsPointOnCurveGCP(curve, point);
                    if (isPointOnCurve)
                        splitPoints.Add(point);
                }
            }
            return splitPoints;
        }
    }
}
