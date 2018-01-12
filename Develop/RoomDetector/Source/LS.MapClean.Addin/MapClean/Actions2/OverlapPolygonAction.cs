using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Algorithms;
using TopologyTools.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.MapClean.Actions2
{
    public class OverlapPolygonInfo
    {
        public IList<Point3d> Positions { get; set; }
        public ObjectId PolylineId { get; set; }

        public OverlapPolygonInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    /// <summary>
    /// 多边形重叠多边形，是在图解法时，不经意间引入的。
    /// 比如在图中
    /// A B 两个多边形，在C处发生了重叠
    /// 
    /// 本算法利用NTS算法和CAD造区算法，找到图中两个多边形存在重叠的区域。
    ///  _____________
    /// |             | 
    /// |            _|_________
    /// |            |  |       |
    /// |     A      |C |  B    |
    /// |            |  |       |
    /// |            |__|_______|
    /// |             |  
    /// |             |  
    /// |_____________|
    /// 
    /// </summary>
    public class OverlapPolygonHandler : AlgorithmWithEditor
    {
        private double _tolerance = Tolerance.Global.EqualPoint;

        public OverlapPolygonHandler(Editor editor)
            : base(editor)
        {
        }

        private PolygonOverlaps _errors;
        public PolygonOverlaps Errors
        {
            get { return _errors; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            _errors = GetOverlapPolygonObjectIds(db, selectedObjectIds, _tolerance);
        }

        public PolygonOverlaps GetOverlapPolygonObjectIds(Database database, 
            IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var singleTopologyResults = OverlapPolygonDetector.FindPolygonOverlaps(selectedObjectIds.ToArray());
            return singleTopologyResults;
        }
    }

    public class OverlapPolygonCheckResult : CheckResult
    {
        public OverlapPolygonCheckResult(ObjectId id1, ObjectId id2, Region region)
            : base(ActionType.OverlapPolygon, new List<ObjectId>() { id1, id2 })
        {
            Region = region;
            DBObjectCollection curves = new DBObjectCollection();
            if (Region != null)
            {
                //Region.Explode(curves);
                //foreach (Curve curve in curves)
                //    _curves.Add(curve);
            }

            HighlightEntity = false;
        }

        IList<Point3d> Positions = new List<Point3d>() { Point3d.Origin };
        IList<Curve> _curves = new List<Curve>();
        public override Point3d[] MarkPoints
        {
            get { return Positions.ToArray(); }
        }

        public override Point3d Position
        {
            get { return MarkPoints[0]; }
        }

        public Region Region { get; set; }

        public override Drawable[] TransientDrawables
        {
            get { return new [] {(Drawable) Region.Clone()}; }

            //get
            //{
            //    return _curves.Select(it => (Drawable)it.Clone()).ToArray();
            //}
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                if (Region != null)
                    Region.Dispose();
                foreach (var polyline in _curves)
                {
                    polyline.Dispose();
                }
                _curves.Clear();
            }
            _disposed = true;

            // Call base class implementation. 
            base.Dispose(disposing);
        }
    }

    public class OverlapPolygonAction : MapCleanActionBase
    {
        public OverlapPolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.OverlapPolygon; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<OverlapPolygonCheckResult>();
            //var editor = Document.Editor;

            //polygonOverlaps.CannotCreateRegions, "不能造区");
            //polygonOverlaps.CannotBooleanRegions, "不能作布尔运算");
            //topoData.WrongEnvelopeObjects, "包围盒计算错误，可能有重复点");
            //topoData.InvalidObjects, "内部拓扑错误");

            var overlaps = OverlapPolygonDetector.FindPolygonOverlaps(selectedObjectIds.ToArray());
            foreach (var overlap in overlaps.GeometryOverlaps)
            {
                var checkResult = new OverlapPolygonCheckResult(overlap.ThisGeometry, 
                    overlap.ThatGeometry, overlap.IntersectRegion);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var result = checkResult as OverlapPolygonCheckResult;
            if (result == null)
                return Status.Rejected;

            // 实际上没有办法fix
            return Status.NoFixMethod;
        }
    }
}
