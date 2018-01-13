using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Settings;
using LS.MapClean.Addin.Utils;
using QuickGraph;
using TopologyTools.Utils;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace LS.MapClean.Addin.MapClean
{
    public abstract class CheckResult : IDisposable
    {
        protected CheckResult(ActionType type, IEnumerable<ObjectId> sourceIds)
        {
            _actionType = type;
            _sourceIds = sourceIds;
            if (_sourceIds != null && _sourceIds.Any())
            {
                _sourceGeometricExtents = CadUtils.SafeGetGeometricExtents(_sourceIds.ToList());
            }
        }

        /// <summary>
        /// Action type of check result.
        /// </summary>
        private readonly ActionType _actionType;
        public ActionType ActionType
        {
            get { return _actionType; }
        }

        /// <summary>
        /// Check result's status.
        /// </summary>
        private MapClean.Status _status = Status.Pending;
        public MapClean.Status Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaiseStatusChangedEvent();
            }
        }

        /// <summary>
        /// Check result's position
        /// </summary>
        public abstract Point3d[] MarkPoints { get; }

        /// <summary>
        /// Check result's position
        /// </summary>
        public abstract Point3d Position { get; }

        /// <summary>
        /// Check result's transient drawables.
        /// </summary>
        public abstract Drawable[] TransientDrawables { get; }

        /// <summary>
        /// Whether to highlight entity
        /// </summary>
        private bool _highlightEntity = true;
        public bool HighlightEntity
        {
            get { return _highlightEntity; }
            set { _highlightEntity = value; }
        }
        /// <summary>
        /// ObjectIds of check result.
        /// </summary>
        private readonly IEnumerable<ObjectId> _sourceIds;
        public IEnumerable<ObjectId> SourceIds
        {
            get { return _sourceIds; }
        }

        /// <summary>
        /// TargetIds after check result is fixed.
        /// </summary>
        public IEnumerable<ObjectId> TargetIds { get; set; }

        /// <summary>
        /// Geometric Extents of source Object ids.
        /// </summary>
        public Extents3d? GeometricExtents
        {
            get
            {
                return GetCheckResultExtents();
            }
        }

        private Extents3d? _sourceGeometricExtents = null;
        protected virtual Extents3d? GetCheckResultExtents()
        {
            return _sourceGeometricExtents;
        }

        /// <summary>
        /// Status changed event
        /// </summary>
        public event EventHandler<EventArgs> StatusChanged;
        protected void RaiseStatusChangedEvent()
        {
            if (StatusChanged != null)
            {
                StatusChanged(this, new EventArgs());
            }
        }

        private bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
    }

    public static class CheckResultUtils
    {
        public static string ToChineseName(this CheckResult checkResult)
        {
            string result = String.Empty;
            switch (checkResult.ActionType)
            {
                case ActionType.NoneZeroElevation:
                    result = "高程不为0对象";
                    break;
                case ActionType.DuplicateVertexPline:
                    result = "多段线重复点";
                    break;
                case ActionType.DeleteDuplicates:
                    result = "重复对象";
                    break;
                case ActionType.EraseShort:
                    result = "短对象";
                    break;
                case ActionType.BreakCrossing:
                    result = "交叉对象";
                    break;
                case ActionType.ExtendUndershoots:
                    result = "未及点";
                    break;
                case ActionType.ApparentIntersection:
                    result = "外观交点";
                    break;
                case ActionType.SnapClustered:
                    result = "节点簇";
                    break;
                case ActionType.DissolvePseudo:
                    result = "伪节点";
                    break;
                case ActionType.EraseDangling:
                    result = "悬挂对象";
                    break;
                case ActionType.ZeroLength:
                    result = "零长度对象";
                    break;
                case ActionType.ZeroAreaLoop:
                    result = "零面积闭合线";
                    break;
                case ActionType.SmallPolygon:
                    result = "小面积多边形";
                    break;
                case ActionType.UnclosedPolygon:
                    result = "非闭合多段线";
                    break;
                case ActionType.IntersectPolygon:
                    result = "相交多边形";
                    break;
                case ActionType.DuplicatePolygon:
                    result = "重复多边形";
                    break;
                case ActionType.SmallPolygonGap:
                    result = "狭小地块缝隙";
                    break;
                case ActionType.PolygonHole:
                    result = "地块间孔洞";
                    break;
                case ActionType.SelfIntersect:
                    result = "自相交或回头线";
                    break;
                case ActionType.MissingVertexInPolygon:
                    result = "三岔口缺顶点";
                    break;
                case ActionType.SelfIntersect2:
                    result = "自相交";
					break;
                case ActionType.AntiClockwisePolygon:
                    result = "逆时针多边形";
                    break;
                case ActionType.FindDangling:
                    result = "悬挂线";
                    break;
                case ActionType.OverlapPolygon:
                    result = "多边形重叠";
                    break;
                case ActionType.AnnotationOverlap:
                    result = "地块标注重叠";
                    break;
                case ActionType.FindIslandPolygon:
                    result = "未处理孔洞";
                    break;
                case ActionType.ArcSegment:
                    result = "弧段";
                    break;
                case ActionType.RectifyPointDeviation:
                    result = "极近点";
                    break;
                case ActionType.SharpCornerPolygon:
                    result = "狭长角多边形";
                    break;
            }
            return result;
        }
    }

    public class CrossingCheckResult : CheckResult
    {
        //private CurveCrossingPoints _curveCrossingPoints;
        //public CurveCrossingPoints CurveCrossingPoints
        //{
        //    get { return _curveCrossingPoints; }
        //}
        //public CrossingCheckResult(CurveCrossingPoints curveCrossingPoints)
        //    : base(ActionType.BreakCrossing, new List<ObjectId>() { curveCrossingPoints.curveId })
        //{
        //    _curveCrossingPoints = curveCrossingPoints;
        //}

        //public override Point3d[] Positions
        //{
        //    get
        //    {
        //        var points = _curveCrossingPoints.points.Cast<Point3d>();
        //        return points.ToArray();
        //    }
        //}

        private CurveCrossingInfo _crossingInfo;
        public CurveCrossingInfo CrossingInfo
        {
            get { return _crossingInfo; }
        }

        public CrossingCheckResult(CurveCrossingInfo crossingInfos)
            : base(ActionType.BreakCrossing, new ObjectId[] { crossingInfos.SourceId, crossingInfos.TargetId })
        {
            _crossingInfo = crossingInfos;
        }

        public override Point3d[] MarkPoints
        {
            get { return _crossingInfo.IntersectPoints; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class DanglingCheckResult : CheckResult
    {
        private IEnumerable<SEdge<CurveVertex>> _danglingPath;
        public IEnumerable<SEdge<CurveVertex>> DanglingPath
        {
            get { return _danglingPath; }
        }

        public DanglingCheckResult(IEnumerable<SEdge<CurveVertex>> danglingPath)
            : base(ActionType.EraseDangling, GetObjectIdsOfDanglingPath(danglingPath))
        {
            _danglingPath = danglingPath;
        }

        private static IEnumerable<ObjectId> GetObjectIdsOfDanglingPath(IEnumerable<SEdge<CurveVertex>> danglingPath)
        {
            var ids = new HashSet<ObjectId>();
            foreach (var edge in danglingPath)
            {
                if (edge.Source.Id != edge.Target.Id)
                    continue;

                ids.Add(edge.Source.Id);
            }
            return ids;
        }

        public override Point3d[] MarkPoints
        {
            get
            {
                return new Point3d[]
                {
                    _danglingPath.Last().Target.Point
                };
            }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class DuplicateEntitiesCheckResult : CheckResult
    {
        private CurveCrossingInfo _crossingInfo;
        public CurveCrossingInfo CrossingInfo
        {
            get { return _crossingInfo; }
        }

        public DuplicateEntitiesCheckResult(CurveCrossingInfo crossingInfos)
            : base(ActionType.DeleteDuplicates, new ObjectId[] { crossingInfos.SourceId, crossingInfos.TargetId })
        {
            _crossingInfo = crossingInfos;
        }

        public override Point3d[] MarkPoints
        {
            get { return _crossingInfo.IntersectPoints; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }

        //private Point3d _startPoint;
        //private Point3d _endPoint;

        //public DuplicateEntitiesCheckResult(ObjectId sourceId)
        //    : base(ActionType.DeleteDuplicates, new List<ObjectId>() { sourceId })
        //{
        //    var database = Application.DocumentManager.MdiActiveDocument.Database;
        //    using (var transaction = database.TransactionManager.StartTransaction())
        //    {
        //        var curve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
        //        if (curve != null)
        //        {
        //            _startPoint = curve.StartPoint;
        //            _endPoint = curve.EndPoint;
        //        }
        //        transaction.Abort();
        //    }
        //}

        //public override Point3d[] Positions
        //{
        //    get { return new Point3d[] { _startPoint, _endPoint }; }
        //}
    }

    public class ResolveShortLineCheckResult : CheckResult
    {
        private Point3d _position;

        public ResolveShortLineCheckResult(ObjectId sourceId)
            : base(ActionType.EraseShort, new List<ObjectId>() { sourceId })
        {
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var curve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
                if (curve != null)
                    _position = curve.StartPoint;
                transaction.Abort();
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[] { _position }; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class ClusteredNodesCheckResult : CheckResult
    {
        private ClusterNodesInfo _clusteredNodes;
        public ClusterNodesInfo ClusteredNodes
        {
            get { return _clusteredNodes; }
        }

        public ClusteredNodesCheckResult(ClusterNodesInfo clusteredNodes)
            : base(ActionType.SnapClustered, GetObjectIdsOfClusterNodes(clusteredNodes))
        {
            _clusteredNodes = clusteredNodes;
        }

        public override Point3d[] MarkPoints
        {
            get
            {
                return ClusteredNodes.Vertices.Select(it => it.Point).ToArray();
            }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }

        private static IEnumerable<ObjectId> GetObjectIdsOfClusterNodes(ClusterNodesInfo nodes)
        {
            var ids = nodes.Vertices.Select(it => it.Id);
            return ids.Distinct().ToArray();
        }
    }

    public class UnderShootCheckResult : CheckResult
    {
        private IntersectionInfo _intersectionInfo;
        public IntersectionInfo IntersectionInfo
        {
            get { return _intersectionInfo; }
        }

        public UnderShootCheckResult(IntersectionInfo info)
            : base(ActionType.ExtendUndershoots, new ObjectId[]{ info.SourceId, info.TargetId })
        {
            _intersectionInfo = info;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{ _intersectionInfo.IntersectPoint }; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class ApparentIntersectionCheckResult : CheckResult
    {
        private IntersectionInfo _intersectionInfo;
        public IntersectionInfo IntersectionInfo
        {
            get { return _intersectionInfo; }
        }

        public ApparentIntersectionCheckResult(IntersectionInfo info)
            : base(ActionType.ApparentIntersection, new ObjectId[]{info.SourceId, info.TargetId})
        {
            _intersectionInfo = info;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{ _intersectionInfo.IntersectPoint }; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class ZeroLengthCheckResult : CheckResult
    {
        private Point3d _position;

        public ZeroLengthCheckResult(ObjectId sourceId)
            : base(ActionType.ZeroLength, new List<ObjectId>() {sourceId})
        {
            var database = Application.DocumentManager.MdiActiveDocument.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var curve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
                if (curve != null)
                    _position = curve.StartPoint;
                transaction.Abort();
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{_position}; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class ZeroAreaLoopCheckResult : CheckResult
    {
        private Point3d _position;

        public ZeroAreaLoopCheckResult(ObjectId sourceId, Point3d position)
            : base(ActionType.ZeroAreaLoop, new List<ObjectId>() { sourceId })
        {
            _position = position;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{_position}; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class SmallPolygonCheckResult : CheckResult
    {
        private Point3d _position = new Point3d(0, 0, 0);
        public SmallPolygonCheckResult(ObjectId sourceId)
            : base(ActionType.SmallPolygon, new ObjectId[] { sourceId })
        {
            using (var transaction = sourceId.Database.TransactionManager.StartTransaction())
            {
                var curve = transaction.GetObject(sourceId, OpenMode.ForRead) as Curve;
                try
                {
                    var extents = curve.GeometricExtents;
                    _position = extents.MinPoint + (extents.MaxPoint - extents.MinPoint) / 2;
                }
                catch (Exception)
                {
                    _position = curve.StartPoint;
                }
                transaction.Commit();
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{ _position }; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class UnclosedPolygonCheckResult : CheckResult
    {
        private Point3d[] _markPoints;
        public UnclosedPolygonCheckResult(ObjectId sourceId)
            : base(ActionType.UnclosedPolygon, new ObjectId[] {sourceId})
        {
            using (var transaction = sourceId.Database.TransactionManager.StartTransaction())
            {
                _markPoints = CurveUtils.GetCurveEndPoints(sourceId, transaction);
                transaction.Commit();
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return _markPoints; }
        }

        public override Point3d Position
        {
            get { return _markPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class IntersectPolygonCheckResult : CheckResult
    {
        //private Autodesk.AutoCAD.DatabaseServices.Polyline _polylineSource = null;
        //private Autodesk.AutoCAD.DatabaseServices.Polyline _polylineTarget = null;
        private List<Autodesk.AutoCAD.DatabaseServices.Polyline> _intersectionPaths =
            new List<Autodesk.AutoCAD.DatabaseServices.Polyline>();

        private Point3d _position = new Point3d(0,0,0);
        private double _basesize = 1.0;

        public IntersectPolygonCheckResult(PolygonIntersect intersect)
            : base(ActionType.IntersectPolygon, new ObjectId[] { intersect.SourceId, intersect.TargetId })
        {
            _intersectionPaths = intersect.Intersections;

            Extents3d extents = _intersectionPaths[0].GeometricExtents;
            for (int i = 1; i < _intersectionPaths.Count; i++)
            {
                extents.AddExtents(_intersectionPaths[i].GeometricExtents);
            }

            _position = extents.MinPoint + (extents.MaxPoint - extents.MinPoint) / 2;
            _basesize = (extents.MaxPoint - extents.MinPoint).Length;
            HighlightEntity = false;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{_position}; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get
            {
                return _intersectionPaths.Select(it => (Drawable)it.Clone()).ToArray();
            }
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                foreach (var polyline in _intersectionPaths)
                    polyline.Dispose();
                _intersectionPaths.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }
    }

    public class SmallPolygonGapCheckResult : CheckResult
    {
        private Point3d _position = new Point3d(0, 0, 0);
        private double _baseSize = 1.0;

        private List<AcadPolyline> _polylines = new List<AcadPolyline>();

        public SmallPolygonGapCheckResult(PolygonGap gap)
            : base(ActionType.SmallPolygonGap, new ObjectId[] { gap.SourceId, gap.TargetId})
        {
            foreach (var segment in gap.SourceSegments)
            {
                var polyline = CreatePolyline(segment);
                _polylines.Add(polyline);
            }
            foreach (var segment in gap.TargetSegments)
            {
                var polyline = CreatePolyline(segment);
                _polylines.Add(polyline);
            }

            var extents = GetExtents3d();
            _position = extents.Value.MinPoint + (extents.Value.MaxPoint - extents.Value.MinPoint) / 2;
            _baseSize = (extents.Value.MaxPoint - extents.Value.MinPoint).Length;

            HighlightEntity = false;
        }

        private AcadPolyline CreatePolyline(KeyValuePair<Point2d, Point2d> segment)
        {
            var polyline = new AcadPolyline();
            polyline.AddVertexAt(0, segment.Key, 0, 0, 0);
            polyline.AddVertexAt(1, segment.Value, 0, 0, 0);
            return polyline;
        }

        private Extents3d? GetExtents3d()
        {
            if (_polylines.Count <= 0)
                return null;

            var extents = _polylines[0].GeometricExtents;
            for (int i = 1; i < _polylines.Count; i++)
            {
                extents.AddExtents(_polylines[i].GeometricExtents);
            }
            return extents;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{ _position }; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get
            {
                return _polylines.Select(it=>(Drawable)it.Clone()).ToArray(); 
            }
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                foreach (var polyline in _polylines)
                {
                    polyline.Dispose();
                }
                _polylines.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }

    //public class SmallPolygonGapCheckResult : CheckResult
    //{
    //    private Autodesk.AutoCAD.DatabaseServices.Polyline _transientPolyline = null;
    //    private Point3d _position = new Point3d(0,0,0);

    //    public SmallPolygonGapCheckResult(PolygonGap2 gap)
    //        : base(ActionType.SmallPolygonGap, new ObjectId[] { gap.SourceId, gap.TargetId })
    //    {
    //        _transientPolyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
    //        for (int i = 0; i < gap.GapVertices.Length; i++)
    //        {
    //            var point2d = new Point2d(gap.GapVertices[i].X, gap.GapVertices[i].Y);
    //            _transientPolyline.AddVertexAt(i, point2d, 0, 1, 1);
    //        }
    //        _transientPolyline.Closed = true;

    //        var extents = _transientPolyline.GeometricExtents;
    //        _position = extents.MinPoint + (extents.MaxPoint - extents.MinPoint) / 2;
    //        HighlightEntity = false;
    //    }

    //    public override Point3d[] MarkPoints
    //    {
    //        get { return new Point3d[0]; }
    //    }

    //    public override Point3d Position
    //    {
    //        get { return _position; }
    //    }

    //    public override Drawable[] TransientDrawables
    //    {
    //        get { return new Drawable[] {_transientPolyline}; }
    //    }

    //    public override double BaseSize
    //    {
    //        get
    //        {
    //            var extents = _transientPolyline.GeometricExtents;
    //            return (extents.MaxPoint - extents.MinPoint).Length;
    //        }
    //    }

    //    private bool _disposed = false;
    //    protected override void Dispose(bool disposing)
    //    {
    //        if (_disposed)
    //            return;
    //        if (disposing)
    //        {
    //            if (_transientPolyline != null)
    //                _transientPolyline.Dispose();
    //            _transientPolyline = null;
    //        }
    //        _disposed = true;

    //        // Call base class implementation. 
    //        base.Dispose(disposing);
    //    }
    }

    public class PolygonHoleCheckResult : CheckResult
    {
        private Point3d _position = new Point3d(0, 0, 0);
        private double _baseSize = 1.0;

        private List<AcadPolyline> _polylines = new List<AcadPolyline>();

        private Extents3d? _extents = null;

        public PolygonHoleCheckResult(AcadPolyline hole)
            : base(ActionType.PolygonHole, new ObjectId[0])
        {
            _polylines.Add(hole);

            _extents = GetExtents3d();
            _position = _extents.Value.MinPoint + (_extents.Value.MaxPoint - _extents.Value.MinPoint) / 2;
            _baseSize = (_extents.Value.MaxPoint - _extents.Value.MinPoint).Length;

            HighlightEntity = false;
        }

        private Extents3d? GetExtents3d()
        {
            if (_polylines.Count <= 0)
                return null;

            var extents = _polylines[0].GeometricExtents;
            for (int i = 1; i < _polylines.Count; i++)
            {
                extents.AddExtents(_polylines[i].GeometricExtents);
            }
            return extents;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[] { _position }; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get
            {
                return _polylines.Select(it => (Drawable)it.Clone()).ToArray();
            }
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                foreach (var polyline in _polylines)
                {
                    polyline.Dispose();
                }
                _polylines.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }

        protected override Extents3d? GetCheckResultExtents()
        {
            return _extents;
        }
    }

    public class SelfIntersectCheckResult : CheckResult
    {
        private CurveCrossingInfo _crossingInfo;
        public CurveCrossingInfo CrossingInfo
        {
            get { return _crossingInfo; }
        }

        public SelfIntersectCheckResult(CurveCrossingInfo crossingInfo)
            : base(ActionType.SelfIntersect, new ObjectId[] { crossingInfo.SourceId})
        {
            _crossingInfo = crossingInfo;
        }

        public override Point3d[] MarkPoints
        {
            get { return _crossingInfo.IntersectPoints; }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class AntiClockwisePolygonCheckResult : CheckResult
    {
        private Point3d _position = new Point3d(0, 0, 0);

        public AntiClockwisePolygonCheckResult(ObjectId curveId)
            : base(ActionType.AntiClockwisePolygon, new ObjectId[] { curveId })
        {
            var extents = GeometryUtils.SafeGetGeometricExtents(curveId);
            if (extents != null)
            {
                var minPoint = extents.Value.MinPoint;
                var maxPoint = extents.Value.MaxPoint;

                _position = minPoint + (maxPoint - minPoint)/2.0;
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[0]; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    
    public class AnnotationOverlapCheckResult : CheckResult
    {
        private List<AcadPolyline> _intersectionPaths = new List<AcadPolyline>();

        private Point3d _position = new Point3d(0,0,0);
        private Extents3d _extents;

        public AnnotationOverlapCheckResult(PolygonIntersect intersect)
            : base(ActionType.AnnotationOverlap, new ObjectId[0])
        {
            using (var transaction = intersect.SourceId.Database.TransactionManager.StartTransaction())
            {
                var polyline = transaction.GetObject(intersect.SourceId, OpenMode.ForRead) as AcadPolyline;
                if(polyline != null)
                    _intersectionPaths.Add((AcadPolyline)polyline.Clone());
                polyline = transaction.GetObject(intersect.TargetId, OpenMode.ForRead) as AcadPolyline;
                if(polyline != null)
                    _intersectionPaths.Add((AcadPolyline)polyline.Clone());
            }

            _extents = _intersectionPaths[0].GeometricExtents;
            for (int i = 1; i < _intersectionPaths.Count; i++)
            {
                _extents.AddExtents(_intersectionPaths[i].GeometricExtents);
            }

            _position = _extents.MinPoint + (_extents.MaxPoint - _extents.MinPoint) / 2;
            HighlightEntity = false;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[0]; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get
            {
                return _intersectionPaths.Select(it => (Drawable)it.Clone()).ToArray();
            }
        }

        protected override Extents3d? GetCheckResultExtents()
        {
            return _extents;
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                foreach (var polyline in _intersectionPaths)
                    polyline.Dispose();
                _intersectionPaths.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }
    }

    public class ArcSegmentCheckResult : CheckResult
    {
        private List<Curve> _arcs = new List<Curve>(); 
        private Point3d _position = new Point3d(0,0,0);
        private Extents3d _extents;

        public ArcSegmentCheckResult(ObjectId curveId, Curve[] arcs)
            : base(ActionType.ArcSegment, new ObjectId[] { curveId})
        {
            _arcs.AddRange(arcs);
            using (var transaction = curveId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)transaction.GetObject(curveId, OpenMode.ForRead);
                _extents = entity.GeometricExtents;
                _position = _extents.MinPoint + (_extents.MaxPoint - _extents.MinPoint) / 2;
                HighlightEntity = false;
            }
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[0]; }
        }

        public override Point3d Position
        {
            get { return _position; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return _arcs.Select(it => (Drawable)it.Clone()).ToArray(); }
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                foreach (var curve in _arcs)
                    curve.Dispose();
                _arcs.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }
    }

    public class NearVerticesCheckResult : CheckResult
    {
        private IEnumerable<CurveVertex> _nearVertices = null;
        public IEnumerable<CurveVertex> NearVertices
        {
            get { return _nearVertices; }
        }

        public NearVerticesCheckResult(IEnumerable<CurveVertex> nearVertices)
            : base(ActionType.RectifyPointDeviation, new List<ObjectId>(nearVertices.Select(it=>it.Id).Distinct()))
        {
            _nearVertices = nearVertices;
        }

        public override Point3d[] MarkPoints
        {
            get { return new Point3d[]{ _nearVertices.First().Point }; }
        }

        public override Point3d Position
        {
            get { return _nearVertices.First().Point; }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }

    public class SharpCornerCheckResult : CheckResult
    {
        private IEnumerable<Point3d> _sharpCorners;

        public SharpCornerCheckResult(ObjectId polygonId, IEnumerable<Point3d> sharpCorners)
            : base(ActionType.SharpCornerPolygon, new List<ObjectId>(){ polygonId })
        {
            _sharpCorners = sharpCorners;
        }

        public override Point3d[] MarkPoints
        {
            get { return _sharpCorners.ToArray(); }
        }

        public override Point3d Position
        {
            get { return _sharpCorners.First(); }
        }

        public override Drawable[] TransientDrawables
        {
            get { return new Drawable[0]; }
        }
    }
}
