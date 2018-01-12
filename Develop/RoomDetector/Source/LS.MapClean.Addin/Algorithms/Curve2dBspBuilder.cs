using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public struct CurveCrossingInfo
    {
        public CurveCrossingInfo(ObjectId sourceId, ObjectId targetId, Point3d[] intersectPoints)
        {
            _intersectPoints = intersectPoints;
            _sourceId = sourceId;
            _targetId = targetId;
        }

        private ObjectId _sourceId;
        public ObjectId SourceId
        {
            get { return _sourceId; }
        }

        private ObjectId _targetId;
        public ObjectId TargetId
        {
            get { return _targetId; }
        }

        private Point3d[] _intersectPoints;
        public Point3d[] IntersectPoints
        {
            get { return _intersectPoints; }
        }
    }


    class Curve2dBspBuilder
    {
        
        /// <summary>
        /// AutoCAD database transaction.
        /// </summary>
        private Transaction _transaction;

        /// <summary>
        /// Bsp Tree which will be built.
        /// </summary>
        private Curve2dBspTree _bspTree;
        public Curve2dBspBuilder(IEnumerable<ObjectId> allIds, Transaction transaction)
        {
            if (allIds == null || !allIds.Any())
                throw new ArgumentNullException("allIds");
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            _transaction = transaction;
            BuildBspTree(allIds);
        }

        public Curve2dBspBuilder(IEnumerable<BspSegment> segments, Transaction transaction)
        {
            if (segments == null || !segments.Any())
                throw new ArgumentNullException("segments");
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            _transaction = transaction;
            _bspTree = new Curve2dBspTree(segments);
        }

        private void BuildBspTree(IEnumerable<ObjectId> allIds )
        {
            var allBspSegments = new List<BspSegment>();
            foreach (var id in allIds)
            {
                var curve = _transaction.GetObject(id, OpenMode.ForRead) as Curve;
                if (curve == null)
                    continue;
                
                var segments = CurveUtils.GetSegment2dsOfCurve(curve, _transaction);
                var bspSegments = segments.Select(it => new BspSegment()
                {
                    EntityId = id,
                    LineSegment = it
                });
                allBspSegments.AddRange(bspSegments);
            }
            _bspTree = new Curve2dBspTree(allBspSegments);
        }

        public IEnumerable<CurveCrossingInfo> SearchRealIntersections(bool includeInline, out IEnumerable<CurveCrossingInfo> duplicateEntities)
        {
            duplicateEntities = new List<CurveCrossingInfo>();
            if (_bspTree.Root == null)
                return new CurveCrossingInfo[0];

            // For self intersection.
            var vertexIntersects = new HashSet<CurveVertex>();

            // Traverse bsp tree to search real intersection
            List<IntersectionInfo> intersections = new List<IntersectionInfo>();
            SearchRealIntersectionsOfNode(_bspTree.Root, intersections, vertexIntersects, includeInline);

            var crossInfos = GetCrossingInfosFromIntersections(intersections);
            
            var result = FilterCrossInfos(crossInfos, out duplicateEntities);
            return result;
        }

        public IEnumerable<IntersectionInfo> SearchRealIntersections(bool includeInline)
        {
            // For self intersection.
            var vertexIntersects = new HashSet<CurveVertex>();

            // Traverse bsp tree to search real intersection
            var intersections = new List<IntersectionInfo>();
            SearchRealIntersectionsOfNode(_bspTree.Root, intersections, vertexIntersects, includeInline);
            return intersections;
        }

        public IEnumerable<CurveCrossingInfo> SearchDuplicateEntities()
        {
            // For self intersection.
            var vertexIntersects = new HashSet<CurveVertex>();

            // Traverse bsp tree to search real intersection
            List<IntersectionInfo> intersections = new List<IntersectionInfo>();
            SearchRealIntersectionsOfNode(_bspTree.Root, intersections, vertexIntersects, includeInline: true);

            var crossInfos = GetCrossingInfosFromIntersections(intersections);
            var result = GetDuplicateEntities(crossInfos);
            return result;
        }

        
        /// <summary>
        /// Search real intersections of bsp node
        /// </summary>
        /// <param name="node"></param>
        private void SearchRealIntersectionsOfNode(CurveBspNode node, List<IntersectionInfo> intersections, HashSet<CurveVertex> vertexIntersects, bool includeInline)
        {
            var segments = node.Segments;
            foreach (var bspSegment in segments)
            {
                if (bspSegment.StartSplitInfos != null)
                {
                    foreach (var splitInfo in bspSegment.StartSplitInfos)
                    {
                        var intersection = CreateRealIntersectionInfo(splitInfo,
                        bspSegment.LineSegment.StartPoint, bspSegment.EntityId, vertexIntersects);
                        
                        if (intersection != null)
                            intersections.Add(intersection);
                    }
                }
                if (bspSegment.EndSplitInfos != null)
                {
                    foreach (var splitInfo in bspSegment.EndSplitInfos)
                    {
                        var intersection = CreateRealIntersectionInfo(splitInfo, 
                            bspSegment.LineSegment.EndPoint, bspSegment.EntityId, vertexIntersects);
                        
                        if (intersection != null)
                            intersections.Add(intersection);
                    }
                }
            }

            // Inline segments's comparasion
            if (segments.Length > 1 && includeInline)
            {
                var inlineIntersections = InlineSegmentsIntersections(segments);
                intersections.AddRange(inlineIntersections);
            }

            if (node.LeftChild != null)
                SearchRealIntersectionsOfNode(node.LeftChild, intersections, vertexIntersects, includeInline);

            if (node.RightChild != null)
                SearchRealIntersectionsOfNode(node.RightChild, intersections, vertexIntersects, includeInline);
        }

        private IntersectionInfo CreateRealIntersectionInfo(BspSplitInfo info, Point2d point, ObjectId targetId, HashSet<CurveVertex> vertexIntersects)
        {
            var extendType = CurveIntersectUtils.ParamToExtendTypeForLine(info.SourceParam);
            if (extendType != ExtendType.None)
                return null;

            // Self intersection
            if ((info.SourceParam.EqualsWithTolerance(0.0) || info.SourceParam.EqualsWithTolerance(1.0)) &&
                info.SourceSegment.EntityId == targetId)
            { 
                // Need to take care of a special case:
                // Polyline AEB and CED intersect at E point
                //     A\  D
                //       \ |
                //        \|_________B
                //      C / E 

                var vertexIntersect = new CurveVertex(new Point3d(point.X, point.Y, 0.0), info.SourceSegment.EntityId);
                // More than two intersects, then it's a self intersection.
                if (vertexIntersects.Contains(vertexIntersect))
                {
                    var intersection = new IntersectionInfo(info.SourceSegment.EntityId, ExtendType.None, targetId,
                        ExtendType.None, new Point3d(point.X, point.Y, 0.0));

                    return intersection;
                }

                vertexIntersects.Add(vertexIntersect);
                return null;
            }
            else
            {
                var intersection = new IntersectionInfo(info.SourceSegment.EntityId, ExtendType.None, targetId,
                    ExtendType.None, new Point3d(point.X, point.Y, 0.0));

                return intersection;
            }
        }

        private IEnumerable<IntersectionInfo> InlineSegmentsIntersections(BspSegment[] segments)
        {
            var result = new List<IntersectionInfo>();
            for (int i = 0; i < segments.Length; i++)
            {
                var currentLineSeg = segments[i].LineSegment;
                var startPoint = currentLineSeg.StartPoint;
                var endPoint = currentLineSeg.EndPoint;

                for(int j = 0; j < segments.Length; j++)
                {
                    if (i == j)
                        continue;

                    var nextLineSeg = segments[j].LineSegment;

                    bool checkStart = (segments[i].StartSplitInfos == null);
                    if (segments[i].StartSplitInfos != null && segments[i].StartSplitInfos.Length > 0)
                    {
                        checkStart = !segments[i].StartSplitInfos[0].IsSplitted;
                    }

                    // Original point if segments[i].StartSplitInfo is null.
                    if (checkStart)
                    {
                        var startParam = nextLineSeg.GetParameterOf(startPoint);
                        if ((startParam.EqualsWithTolerance(0.0) || startParam.EqualsWithTolerance(1.0))
                            && i < j && segments[i].EntityId != segments[j].EntityId)
                        {
                            result.Add(new IntersectionInfo(segments[i].EntityId, ExtendType.None,
                                segments[j].EntityId, ExtendType.None, new Point3d(startPoint.X, startPoint.Y, 0.0)));
                        }
                        else if (startParam.Larger(0.0) && startParam.Smaller(1.0))
                        {
                            result.Add(new IntersectionInfo(segments[i].EntityId, ExtendType.None,
                                segments[j].EntityId, ExtendType.None, new Point3d(startPoint.X, startPoint.Y, 0.0)));
                        }
                    }

                    bool checkEnd = (segments[i].EndSplitInfos == null);
                    if (segments[i].EndSplitInfos != null && segments[i].EndSplitInfos.Length > 0)
                    {
                        checkStart = !segments[i].EndSplitInfos[0].IsSplitted;
                    }

                    // Original point if segments[i].EndSplitInfo is null.
                    if (segments[i].EndSplitInfos == null)
                    {
                        var endParam = nextLineSeg.GetParameterOf(endPoint);
                        if ((endParam.EqualsWithTolerance(0.0) || endParam.EqualsWithTolerance(1.0))
                            && i < j && segments[i].EntityId != segments[j].EntityId)
                        {
                            result.Add(new IntersectionInfo(segments[i].EntityId, ExtendType.None,
                                segments[j].EntityId, ExtendType.None, new Point3d(endPoint.X, endPoint.Y, 0.0)));
                        }
                        else if (endParam.Larger(0.0) && endParam.Smaller(1.0))
                        {
                            result.Add(new IntersectionInfo(segments[i].EntityId, ExtendType.None,
                                segments[j].EntityId, ExtendType.None, new Point3d(endPoint.X, endPoint.Y, 0.0)));
                        }
                    }
                }
            }
            return result;
        }

        private IEnumerable<CurveCrossingInfo> GetCrossingInfosFromIntersections(List<IntersectionInfo> infos)
        {
            //var result = new List<CurveCrossingInfo>();
            //var currentInfos = infos;
            //while (currentInfos.Any())
            //{
            //    var info = currentInfos.First();
            //    var group = currentInfos.Where(it =>
            //        (it.SourceId == info.SourceId && it.TargetId == info.TargetId) ||
            //        (it.TargetId == info.SourceId && it.SourceId == info.TargetId));
               
            //    var points = new HashSet<Point3d>();
            //    foreach (var intersection in group)
            //    {
            //        points.Add(intersection.IntersectPoint);
            //    }

            //    if (points.Count > 0)
            //    {
            //        var crossingInfo = new CurveCrossingInfo(info.SourceId, info.TargetId, points.ToArray());
            //        result.Add(crossingInfo);
            //    }
            //    currentInfos = currentInfos.Except(group);
            //}
            //return result;

            var result = new List<CurveCrossingInfo>();
            var sourceIdGroups = infos.GroupBy(it => it.SourceId);
            HashSet<KeyValuePair<ObjectId, ObjectId>> visited = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            foreach (var sourceIdGroup in sourceIdGroups)
            {
                var subSourceIdGroups = sourceIdGroup.GroupBy(it => it.TargetId);
                foreach (var subSourceIdGroup in subSourceIdGroups)
                {
                    if (visited.Contains(new KeyValuePair<ObjectId, ObjectId>(subSourceIdGroup.Key, sourceIdGroup.Key)))
                        continue;

                    var points = new List<Point3d>();
                    foreach (var intersection in subSourceIdGroup)
                    {
                        if (points.Contains(intersection.IntersectPoint))
                            continue;

                        points.Add(intersection.IntersectPoint);
                    }

                    // Search the target group by TargetId.
                    var targetGroup = sourceIdGroups.FirstOrDefault(it => it.Key == subSourceIdGroup.Key);
                    if (targetGroup != null)
                    {
                        // Continue group by SourceId (the TargetId in targetIdGroups)
                        var subTargetGroups = targetGroup.GroupBy(it => it.TargetId);
                        var subTargetGroup = subTargetGroups.FirstOrDefault(it => it.Key == sourceIdGroup.Key);
                        if (subTargetGroup != null)
                        {
                            foreach (var intersection in subTargetGroup)
                            {
                                if (points.Contains(intersection.IntersectPoint))
                                    continue;
                                points.Add(intersection.IntersectPoint);
                            }
                        }
                    }

                    if (points.Count > 0)
                    {
                        var crossingInfo = new CurveCrossingInfo(sourceIdGroup.Key, subSourceIdGroup.Key, points.ToArray());
                        result.Add(crossingInfo);
                    }

                    visited.Add(new KeyValuePair<ObjectId, ObjectId>(sourceIdGroup.Key, subSourceIdGroup.Key));
                }
            }

            return result;
        }

        private IEnumerable<CurveCrossingInfo> FilterCrossInfos(IEnumerable<CurveCrossingInfo> crossInfos, out IEnumerable<CurveCrossingInfo> duplicateEntities )
        {
            duplicateEntities = new List<CurveCrossingInfo>();
            var result = new List<CurveCrossingInfo>();
            foreach (var curveCrossingInfo in crossInfos)
            {
                // Filter out duplicate entities
                if (AreDuplicateEntities(curveCrossingInfo))
                {
                    ((List<CurveCrossingInfo>)duplicateEntities).Add(curveCrossingInfo);
                    continue;
                }

                var sourcePoints = CurveUtils.GetCurveEndPoints(curveCrossingInfo.SourceId, _transaction);
                var targetPoints = new Point3d[0];
                if (curveCrossingInfo.TargetId != curveCrossingInfo.SourceId)
                    targetPoints = CurveUtils.GetCurveEndPoints(curveCrossingInfo.TargetId, _transaction);
                sourcePoints = DistinctEndPoints(sourcePoints);
                targetPoints = DistinctEndPoints(targetPoints);

                var points = new List<Point3d>();
                foreach (var point3D in curveCrossingInfo.IntersectPoints)
                {
                    // Whether point3D is end point of each cuve
                    // If sourcePoints.Length is 1 or 0, means it's a loop, loop need to be splitted.
                    if (sourcePoints.Length >= 2 && sourcePoints.Contains(point3D) && 
                        targetPoints.Length >= 2 && targetPoints.Contains(point3D))
                        continue;

                    points.Add(point3D);
                }
                if (points.Count > 0)
                {
                    var newCrossingInfo = new CurveCrossingInfo(curveCrossingInfo.SourceId, curveCrossingInfo.TargetId,
                        points.ToArray());
                    result.Add(newCrossingInfo);
                }
            }
            return result;
        }

        private Point3d[] DistinctEndPoints(Point3d[] endPoints)
        {
            if (endPoints.Length != 2)
                return endPoints;
            if (endPoints[0] == endPoints[1])
                return new Point3d[]{ endPoints[0] };
            return endPoints;
        }

        private bool AreDuplicateEntities(CurveCrossingInfo crossInfo)
        {
            if (crossInfo.SourceId == crossInfo.TargetId)
                return false;

            // Check whether all the intersection points are curve's vertices.
            var sourceVertices = CurveUtils.GetDistinctVertices(crossInfo.SourceId, _transaction);
            var targetVertices = CurveUtils.GetDistinctVertices(crossInfo.TargetId, _transaction);
            var sourceCount = sourceVertices.Count();
            var targetCount = targetVertices.Count();
            if (sourceCount != targetCount)
                return false;

            if (sourceCount != crossInfo.IntersectPoints.Length)
                return false;

            foreach (var point in crossInfo.IntersectPoints)
            {
                if (!sourceVertices.Contains(point) || !targetVertices.Contains(point))
                    return false;
            }
            return true;
        }

        private IEnumerable<CurveCrossingInfo> GetDuplicateEntities(IEnumerable<CurveCrossingInfo> crossInfos)
        {
            var result = new List<CurveCrossingInfo>();
            foreach (var curveCrossingInfo in crossInfos)
            {
                if (AreDuplicateEntities(curveCrossingInfo))
                    result.Add(curveCrossingInfo);
            }

            return result;
        }
    }
}
