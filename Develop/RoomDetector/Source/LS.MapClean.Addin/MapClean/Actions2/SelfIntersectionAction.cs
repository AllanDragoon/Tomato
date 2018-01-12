using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Algorithms;
using TopologyTools.Utils;

namespace LS.MapClean.Addin.MapClean.Actions2
{
    public class SelfIntersectionInfo
    {
        public IList<Point3d> Positions { get; set; }
        public ObjectId PolylineId { get; set; }

        public SelfIntersectionInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    /// <summary>
    /// 自交多边形，是在图解法时，不经意间引入的。
    /// 比如在图中
    /// 实际测量，或者图解的时候，需要在V1,V2,V3处，发生了自交
    /// 删除V2顶点，需要保留V1,V3连线；
    /// 或者保留V1, V2, V3，删除V1, V3连线
    /// 
    /// 另外一种常见的自交是回头线。
    /// 
    /// 本算法利用NTS的快速计算节点的算法，找到图中存在自交的线段的位置，并自动添加。
    ///  _____________
    /// |             | 
    /// |             V1___V2
    /// |             \   /
    /// |              \ /
    /// |               V3 
    /// |              /
    /// |             |
    /// |             |
    /// |_____________|
    /// 
    /// </summary>
    public class SelfIntersectionInPolygonHandler : AlgorithmWithEditor
    {
        private double _tolerance = Tolerance.Global.EqualPoint;

        public SelfIntersectionInPolygonHandler(Editor editor)
            : base(editor)
        {
            _errorObjectIds = new Dictionary<ObjectId, SingleTopologyError>();
        }

        private Dictionary<ObjectId, SingleTopologyError> _errorObjectIds;
        public Dictionary<ObjectId, SingleTopologyError> TopologyErrors
        {
            get { return _errorObjectIds; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            _errorObjectIds = GetSelfIntersectionObjectIds(db, selectedObjectIds, _tolerance);
        }

        public Dictionary<ObjectId, SingleTopologyError> GetSelfIntersectionObjectIds(Database database, 
            IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var singleTopologyResults = SingleTopologyValidator.CheckValid(selectedObjectIds.ToList());
            return singleTopologyResults;
        }
    }

    public class SelfIntersectionInPolygonCheckResult : CheckResult
    {
        public SelfIntersectionInPolygonCheckResult(ObjectId sourceId, IList<Point3d> points)
            : base(ActionType.SelfIntersect2, new List<ObjectId>() { sourceId })
        {
            ObjectId = sourceId;
            Positions = points;
        }

        public ObjectId ObjectId { get; set; }
        IList<Point3d> Positions = new List<Point3d>();

        public override Point3d[] MarkPoints
        {
            get { return Positions.ToArray(); }
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

    public class SelfIntersectionInPolygonAction : MapCleanActionBase
    {
        public SelfIntersectionInPolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.SelfIntersect2; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var results = new List<SelfIntersectionInPolygonCheckResult>();
            var editor = Document.Editor;
            var handler = new SelfIntersectionInPolygonHandler(editor);
            handler.Check(selectedObjectIds);
            if (handler.TopologyErrors == null)
                return results;

            foreach (KeyValuePair<ObjectId, SingleTopologyError> keyValue in handler.TopologyErrors)
            {
                if (keyValue.Value.ErrorType == SingleTopologyErrors.SelfIntersection)
                {
                    var errorInfo = SingleTopologyValidator.LineStringSelfIntersectionsOp(keyValue.Key);
                    var checkResult = new SelfIntersectionInPolygonCheckResult(keyValue.Key, errorInfo);
                    results.Add(checkResult);
                }
                else
                {
                    Document.Editor.WriteMessage("\n" + keyValue.Value.Message);
                }
            }

            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var result = checkResult as SelfIntersectionInPolygonCheckResult;
            if (result == null)
                return Status.Rejected;

            // 实际上没有办法fix
            return Status.NoFixMethod;
        }
    }
}
