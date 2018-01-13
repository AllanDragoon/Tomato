using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using LS.MapClean.Addin.Algorithms;
using TopologyTools;

namespace LS.MapClean.Addin.MapClean.Actions2
{
    public class MissingVertexInfo
    {
        public IList<Point3d> Positions { get; set; }
        public ObjectId PolylineId { get; set; }

        public MissingVertexInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    /// <summary>
    /// 三岔口少顶点，是在图解法时，不经意间引入的，特别是在相邻多边形的交点处，很容易发生。
    /// 比如在图中
    ///      勾绘大地块A时，会有4个顶点
    ///      勾绘小地块B时，会有4个顶点
    ///      勾绘小地块C时，会有4个顶点
    /// 实际测量，或者图解的时候，需要在V1,V2,V3处，为A地块增加三个节点。
    /// 本算法利用NTS的快速计算节点的算法，找到图中未添加的顶点，并自动添加。
    /// 
    /// o-------------o
    /// |             |
    /// |             |
    /// |             V1--------o
    /// |             |         | 
    /// |             |    B    |
    /// |     A       |         |
    /// |             V2--------o
    /// |             |         |
    /// |             |    C    |
    /// |             |         |
    /// |             V3--------o
    /// |             |
    /// o-------------o
    /// 
    /// 使用“这个命令”可少端点的的多段线，并在节点处自动增加顶点。
    /// </summary>
    public class MissingVertexInPolygonHandler : AlgorithmWithEditor
    {
        private double _tolerance = Tolerance.Global.EqualPoint;

        public MissingVertexInPolygonHandler(Editor editor)
            : base(editor)
        {
            _missingVertexCollection = new Dictionary<ObjectId, IList<Point3d>>();
        }

        private Dictionary<ObjectId, IList<Point3d>> _missingVertexCollection;
        public Dictionary<ObjectId, IList<Point3d>> MissingVertexCollection
        {
            get { return _missingVertexCollection; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            _missingVertexCollection = GetMissingVertexObjectIds(db, selectedObjectIds, _tolerance);
        }

        public Dictionary<ObjectId, IList<Point3d>> GetMissingVertexObjectIds(Database database, 
            IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var polylineNodingResult = PolylineNoder.FindMissingVertex(selectedObjectIds.ToList(), tolerance);
            return polylineNodingResult;
        }
    }

    public class MissingVertexInPolygonCheckResult : CheckResult
    {
        public MissingVertexInPolygonCheckResult(ObjectId sourceId, IList<Point3d> points)
            : base(ActionType.MissingVertexInPolygon, new List<ObjectId>() { sourceId })
        {
            ObjectId = sourceId;
            MissingPositions = points;
        }

        public ObjectId ObjectId { get; set; }
        public IList<Point3d> MissingPositions { get; set; }

        public override Point3d[] MarkPoints
        {
            get { return MissingPositions.ToArray(); }
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

    public class MissingVertexInPolygonAction : MapCleanActionBase
    {
        public MissingVertexInPolygonAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.MissingVertexInPolygon; }
        }

        public override bool Hasparameters
        {
            get { return false; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            //var results = new List<MissingVertexInPolygonCheckResult>();
            //var editor = Document.Editor;
            //var handler = new MissingVertexInPolygonHandler(editor);
            //handler.Check(selectedObjectIds);
            //if (handler.MissingVertexCollection == null)
            //    return results;

            //foreach (KeyValuePair<ObjectId, IList<Point3d>> keyValue in handler.MissingVertexCollection)
            //{
            //    var checkResult = new MissingVertexInPolygonCheckResult(keyValue.Key, keyValue.Value);
            //    results.Add(checkResult);
            //}

            //return results;

            // Allan：用上述算法在某些情况下，三岔点查找不到，因此改用新算法，并且速度更快。
            var results = new List<MissingVertexInPolygonCheckResult>();
            var searcher = new MissingVertexSearcherQuadTree(Document.Editor);
            searcher.Check(selectedObjectIds);
            foreach (var info in searcher.MissingVertexInfos)
            {
                var checkResult = new MissingVertexInPolygonCheckResult(info.PolylineId, info.Positions);
                results.Add(checkResult);
            }
            return results;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var result = checkResult as MissingVertexInPolygonCheckResult;
            if (result == null)
                return Status.Rejected;

            // Fix 就是 在缺点的地方添加顶点
            using (var tr = Document.Database.TransactionManager.StartTransaction())
            {
                var entity = tr.GetObject(result.ObjectId, OpenMode.ForWrite) as Curve;
                if (entity != null)
                {
                    foreach (var position in result.MissingPositions)
                    {
                        AddVertex.AddVertexFromPolyline(tr, entity, position); 
                    }
                }
                   
                tr.Commit();
            }
            return Status.Fixed;
        }
    }
}
