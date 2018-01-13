using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using DbxUtils.Utils;
using LS.MapClean.Addin.Algorithms;
using TopologyTools.Utils;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace LS.MapClean.Addin.MapClean.Actions2
{
    /// <summary>
    /// 悬挂线，是在图解法时，不经意间引入的。
    /// 比如在图中
    /// 实际测量，或者图解的时候，需要在V1,V2,V3处，发生了悬挂线，比如V2,V3就是悬挂线
    /// 
    /// 本算法利用NTS的快速计算节点的算法，找到图中存在悬挂线的线段的位置
    ///  _____________
    /// |             | 
    /// |      _      | 
    /// |     / \     |
    /// |    /___\    |
    /// |             |
    /// |             |
    /// |      __     |
    /// |     [  ]    | 
    /// |     [__]    |
    /// |_____________|
    /// 
    /// </summary>
    public class FindIslandPolygonHandler : AlgorithmWithEditor
    {
        public FindIslandPolygonHandler(Editor editor)
            : base(editor)
        {
        }

        public List<ObjectId> HoleIds { get; set; }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
        }
    }

    public class FindIslandPolygonCheckResult : CheckResult
    {
        private List<AcadPolyline> _polylines = new List<AcadPolyline>();

        public FindIslandPolygonCheckResult(ObjectId sourceId)
            : base(ActionType.FindIslandPolygon, new List<ObjectId>() { sourceId })
        {
            ObjectId = sourceId;

            var boundaryPoints = PolylineUtils1.GetBoundaryPointCollection(sourceId);
            IList<Point3d> positions = new List<Point3d>();
            foreach (Point3d position in boundaryPoints)
                positions.Add(position);
            positions.Add(boundaryPoints[0]);
            _polylines.Add(CreatePolyline(positions));
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

    public class FindIslandPolygonAction : MapCleanActionBase
    {
        public FindIslandPolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.FindIslandPolygon; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<FindIslandPolygonCheckResult>();
            var editor = Document.Editor;

            var document = Application.DocumentManager.MdiActiveDocument;
            List<ObjectId> holeIds = PolygonHoleHelper.FindUnreferenceHoles(document);
            foreach (var holeId in holeIds)
            {
                var checkResult = new FindIslandPolygonCheckResult(holeId);
                results.Add(checkResult);
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var result = checkResult as FindIslandPolygonCheckResult;
            if (result == null)
                return Status.Rejected;

            // 实际上没有办法fix
            return Status.NoFixMethod;
        }
    }
}
