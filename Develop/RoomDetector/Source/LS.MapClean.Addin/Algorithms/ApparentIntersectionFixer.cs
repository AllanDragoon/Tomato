using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// http://docs.autodesk.com/MAP/2014/CHS/index.html?url=filesMAPLRN/GUID-1C93E885-3623-4969-8374-5E6FD572BB07.htm,topicNumber=MAPLRNd30e135,hash=GUID-EEE3FBB2-8710-45DC-BF98-01AEA2D016AC
    /// With Apparent Intersection, you can locate two objects that do not intersect but that could be extended (within a specified tolerance radius) 
    /// along their natural paths to intersect at a projected point.
    /// </summary>
    class ApparentIntersectionFixer : AlgorithmWithEditor
    {
        private double _tolerance;

        private IEnumerable<IntersectionInfo> _apparentIntersections;
        public IEnumerable<IntersectionInfo> ApparentIntersections
        {
            get { return _apparentIntersections; }
        }

        public ApparentIntersectionFixer(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var watch = Stopwatch.StartNew();

            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Search dangling vertices.
                //var danglingSearcher = new DanglingVertexSearcher(objIds, true, transaction);
                //danglingSearcher.SelectCurves = this.SelectCurvesAtPoint;
                //var danglingVertices = danglingSearcher.Search();

                var danglingSearcher = new KdTreeDanglingVertexSearcher(selectedObjectIds, true, transaction);
                var danglingVertices = danglingSearcher.Search();

                // Traverse all dangling vertices and search the apparent intersection.
                //_apparentIntersections = GetApparentIntersections(danglingVertices, transaction);
                _apparentIntersections = GetApparentIntersectionsKdTree(danglingVertices, transaction);
                
                transaction.Commit();
            }

            // the code that you want to measure comes here
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n查找外观交点花费时间{0}毫秒", elapsedMs);
        }

        private IEnumerable<IntersectionInfo> GetApparentIntersectionsKdTree(IEnumerable<CurveVertex> danglingVertices, Transaction transaction)
        {
            // Create a KDTree for dangling vertices.
            var result = new List<IntersectionInfo>();
            var visitedPairs = new Dictionary<CurveVertex, CurveVertex>();
            var kdTree = new CurveVertexKdTree<CurveVertex>(danglingVertices, it=>it.Point.ToArray(), ignoreZ: true);
            foreach (var danglingVertex in danglingVertices)
            {
                var neighbors = kdTree.NearestNeighbours(danglingVertex.Point.ToArray(), _tolerance*2.0);
                if (!neighbors.Any())
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (visitedPairs.ContainsKey(neighbor))
                        continue;

                    // Mark it as visited.
                    visitedPairs[neighbor] = danglingVertex;
                    var info = CalcApparentIntersection(danglingVertex, neighbor, transaction);
                    if (info != null)
                    { 
                        result.Add(info);
                    }
                }
            }
            return result;
        }

        private IEnumerable<IntersectionInfo> GetApparentIntersections(IEnumerable<CurveVertex> danglingVertices, Transaction transaction)
        {
            var result = new List<IntersectionInfo>();
            var visitedVertices = new HashSet<CurveVertex>();
            foreach (var vertex in danglingVertices)
            {
                if (visitedVertices.Contains(vertex))
                    continue;
                var infos = GetApparentIntersections(vertex, danglingVertices, visitedVertices, transaction);
                result.AddRange(infos);
            }
            return result;
        }

        private IEnumerable<IntersectionInfo> GetApparentIntersections(CurveVertex current, IEnumerable<CurveVertex> danglingVertices,
            HashSet<CurveVertex> visitedVertices, Transaction transaction)
        {
            visitedVertices.Add(current);
            var result = new List<IntersectionInfo>();
            var ids = Editor.SelectEntitiesAtPoint(SelectionFilterUtils.OnlySelectCurve(), current.Point, _tolerance);
            foreach (var id in ids)
            {
                // Check whether danglingVertices contains this id.
                var vertices = danglingVertices.Where(it => it.Id == id);
                if (!vertices.Any())
                    continue;

                // Check each vertex of this id.
                foreach (var vertex in vertices)
                {
                    if (vertex == current)
                        continue;

                    if (visitedVertices.Contains(vertex))
                        continue;

                    var distance = (vertex.Point - current.Point).Length;
                    if (distance.Larger(_tolerance*2))
                        continue;

                    // Calcuate the intersection info of these two vertices.
                    var info = CalcApparentIntersection(current, vertex, transaction);
                    if (info != null)
                    {
                        result.Add(info);
                    }
                }
            }
            
            return result;
        }

        private IntersectionInfo CalcApparentIntersection(CurveVertex source, CurveVertex target, Transaction transaction)
        {
            var sourceEntity = transaction.GetObject(source.Id, OpenMode.ForRead);
            var targetEntity = transaction.GetObject(target.Id, OpenMode.ForRead);

            // Source curve 
            var desiredSourceExtend = CurveUtils.GetExtendType((Curve)sourceEntity, source.Point);
            var sourceLine = sourceEntity as Line;
            var sourceArc = sourceEntity as Arc;
            var sourcePolyline = sourceEntity as Autodesk.AutoCAD.DatabaseServices.Polyline;
            var sourcePolyline2d = sourceEntity as Polyline2d;
            if (sourcePolyline != null)
            {
                sourceLine = CurveUtils.CutOutLineFromPolyLine(sourcePolyline, source.Point);
            }
            else if (sourcePolyline2d != null)
            {
                sourceLine = CurveUtils.CutOutLineFromPolyline2d(sourcePolyline2d, source.Point, transaction);
            }

            // Target curve
            var desiredTargetExtend = CurveUtils.GetExtendType((Curve)targetEntity, target.Point);
            var targetLine = targetEntity as Line;
            var targetArc = targetEntity as Arc;
            var targetPolyline = targetEntity as Autodesk.AutoCAD.DatabaseServices.Polyline;
            var targetPolyline2d = targetEntity as Polyline2d;
            if (targetPolyline != null)
            {
                targetLine = CurveUtils.CutOutLineFromPolyLine(targetPolyline, target.Point);
            }
            else if (targetPolyline2d != null)
            {
                targetLine = CurveUtils.CutOutLineFromPolyline2d(targetPolyline2d, target.Point, transaction);
            }
            
            // Calculate intersection.
            var results = new List<IntersectionInfo>();
            if (sourceLine != null && targetLine != null)
            {
                 // Line && Line
                var info = CurveIntersectUtils.InsersectLines(sourceLine, targetLine);
                if (info != null)
                {
                    info.SourceId = source.Id;
                    info.TargetId = target.Id;
                    results.Add(info);
                }
            }
            else if (sourceLine != null && targetArc != null)
            {
                // Line && Arc
                var infos = CurveIntersectUtils.IntersectLineArc(sourceLine, targetArc, coerceArcExtendType: desiredTargetExtend);
                foreach (var info in infos)
                {
                    info.SourceId = source.Id;
                    info.TargetId = target.Id;
                }
                results.AddRange(infos);
            }
            else if (sourceArc != null && targetLine != null)
            {
                // Arc && Line
                var infos = CurveIntersectUtils.IntersectLineArc(targetLine, sourceArc, coerceArcExtendType: desiredSourceExtend);
                foreach (var info in infos)
                {
                    // Exchange entend type.
                    var newInfo = new IntersectionInfo(source.Id, info.TargetExtendType, target.Id,
                        info.SourceExtendType, info.IntersectPoint);

                    results.Add(newInfo);
                }
            }
            else if (sourceArc != null && targetArc != null)
            {
                // Arc && Arc
                var infos = CurveIntersectUtils.IntersectArcs(sourceArc, desiredSourceExtend, targetArc,
                    desiredTargetExtend);
                foreach (var info in infos)
                {
                    info.SourceId = source.Id;
                    info.TargetId = target.Id;
                }

                results.AddRange(infos);
            }

            IntersectionInfo result = null;
            foreach (var info in results)
            {
                var valid = CheckIntersectionInfo(info, desiredSourceExtend, source.Point, desiredTargetExtend, target.Point, _tolerance);
                if (valid)
                { 
                    result = info;
                    break;
                }
            }
            return result;
        }

        private bool CheckIntersectionInfo(IntersectionInfo info, ExtendType desiredSourceExtendType, Point3d sourcePoint,
            ExtendType desiredTargetExtendType, Point3d targetPoint, double tolerance)
        {
            if (info.SourceExtendType != desiredSourceExtendType)
                return false;
            if (info.TargetExtendType != desiredTargetExtendType)
                return false;
            var dist = (sourcePoint - info.IntersectPoint).Length;
            if (dist.Larger(tolerance))
                return false;
            dist = (targetPoint - info.IntersectPoint).Length;
            if (dist.Larger(tolerance))
                return false;
            return true;
        }
    }

}
