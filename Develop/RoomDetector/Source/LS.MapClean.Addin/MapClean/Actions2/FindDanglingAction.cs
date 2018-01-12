using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Algorithms;
using TopologyTools.Utils;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace LS.MapClean.Addin.MapClean.Actions2
{
    public class FindDanglingInfo
    {
        public IList<Point3d> Positions { get; set; }
        public ObjectId PolylineId { get; set; }

        public FindDanglingInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    /// <summary>
    /// 悬挂线，是在图解法时，不经意间引入的。
    /// 比如在图中
    /// 实际测量，或者图解的时候，需要在V1,V2,V3处，发生了悬挂线，比如V2,V3就是悬挂线
    /// 
    /// 本算法利用NTS的快速计算节点的算法，找到图中存在悬挂线的线段的位置
    ///  _____________
    /// |             | 
    /// |             V1   V2
    /// |             \   /
    /// |              \ /
    /// |               V3 
    /// |              /
    /// |             |
    /// |             |
    /// |_____________|
    /// 
    /// </summary>
    public class FindDanglingHandler : AlgorithmWithEditor
    {
        private double _tolerance = Tolerance.Global.EqualPoint;

        public FindDanglingHandler(Editor editor)
            : base(editor)
        {
            _errorObjectIds = new Dictionary<ObjectId, SingleTopologyError>();
        }

        private Dictionary<ObjectId, SingleTopologyError> _errorObjectIds;
        public Dictionary<ObjectId, SingleTopologyError> TopologyErrors
        {
            get { return _errorObjectIds; }
        }

        Dictionary<ObjectId, IList<Point3d>> _danglingPoints = new Dictionary<ObjectId, IList<Point3d>>();
        public Dictionary<ObjectId, IList<Point3d>> DanglingPoints
        {
            get { return _danglingPoints; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            _danglingPoints = GetFindDanglingPoints(db, selectedObjectIds, _tolerance);
        }

        public Dictionary<ObjectId, SingleTopologyError> GetFindDanglingObjectIds(Database database, 
            IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            //var singleTopologyResults = SingleTopologyValidator.FindDanglingLine(selectedObjectIds.ToList());
            return null;
        }

        public Dictionary<ObjectId, IList<Point3d>> GetFindDanglingPoints(Database database, IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var singleTopologyResults = SingleTopologyValidator.FindDanglingLine(selectedObjectIds.ToList());
            return singleTopologyResults;
        }
    }

    public class FindDanglingCheckResult : CheckResult
    {
        private List<AcadPolyline> _polylines = new List<AcadPolyline>();

        public FindDanglingCheckResult(ObjectId sourceId, IList<Point3d> points)
            : base(ActionType.FindDangling, new List<ObjectId>() { sourceId })
        {
            ObjectId = sourceId;
            Positions = points;

            _polylines.Add(CreatePolyline(points));

            //var extents = GetExtents3d();
            //_position = extents.Value.MinPoint + (extents.Value.MaxPoint - extents.Value.MinPoint) / 2;
            //_baseSize = (extents.Value.MaxPoint - extents.Value.MinPoint).Length;
            HighlightEntity = false;
        }

        public ObjectId ObjectId { get; set; }
        IList<Point3d> Positions = new List<Point3d>();

        public override Point3d[] MarkPoints
        {
            get { return Positions.ToArray(); }
        }

        private AcadPolyline CreatePolyline(IList<Point3d> points)
        {
            var polyline = new AcadPolyline();
            foreach (var point3D in points)
            {
                polyline.AddVertexAt(polyline.NumberOfVertices, new Point2d(point3D.X, point3D.Y), 0, 0, 0);
            }
            return polyline;
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public override Drawable[] TransientDrawables
        {
            //get { return new Drawable[0]; }
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
    }

    public class FindDanglingAction : MapCleanActionBase
    {
        public FindDanglingAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.FindDangling; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<FindDanglingCheckResult>();
            var editor = Document.Editor;
            var handler = new FindDanglingHandler(editor);
            handler.Check(selectedObjectIds);

            foreach (var keyValue in handler.DanglingPoints)
            {
                var checkResult = new FindDanglingCheckResult(keyValue.Key, keyValue.Value);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var result = checkResult as FindDanglingCheckResult;
            if (result == null)
                return Status.Rejected;

            // 实际上没有办法fix
            return Status.NoFixMethod;
        }
    }
}
