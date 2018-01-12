using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using TopologyTools;

namespace LS.MapClean.Addin.Algorithms
{
    public class MissingVertexInfo
    {
        public ObjectId PolylineId { get; set; }
        public List<Point3d> Positions { get; set; }

        public MissingVertexInfo()
        {
            Positions = new List<Point3d>();
            PolylineId = ObjectId.Null;
        }
    }

    public class MissingVertexSearcher : AlgorithmWithEditor
    {
        private double _tolerance = 0.000001;
        public double Tolerance
        {
            get { return _tolerance; }
            set { _tolerance = value; }
        }

        private List<MissingVertexInfo> _missingVertexInfos = new List<MissingVertexInfo>();
        public IEnumerable<MissingVertexInfo> MissingVertexInfos
        {
            get { return _missingVertexInfos; }
        } 

        public MissingVertexSearcher(Editor editor, double tolerance)
            : base(editor)
        {
            Tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var database = Editor.Document.Database;
            IEnumerable<IntersectionInfo> intersections = null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Build curve bsp tree and search all intersections
                var curve2dBspBuilder = new Curve2dBspBuilder(selectedObjectIds, transaction);
                intersections = curve2dBspBuilder.SearchRealIntersections(includeInline: true);
                transaction.Commit();
            }

            // Group intersections by object id.
            var group = new Dictionary<ObjectId, List<Point3d>>();
            foreach (var intersection in intersections)
            {
                if(!group.ContainsKey(intersection.SourceId))
                    group[intersection.SourceId] = new List<Point3d>();
                var points = group[intersection.SourceId];
                if(!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);

                if (intersection.SourceId == intersection.TargetId)
                    continue;

                if(!group.ContainsKey(intersection.TargetId))
                    group[intersection.TargetId] = new List<Point3d>();
                points = group[intersection.TargetId];
                if(!points.Contains(intersection.IntersectPoint))
                    points.Add(intersection.IntersectPoint);
            }

            // Check each intersection whether it's a missing vertex.
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var pair in group)
                {
                    var curve = transaction.GetObject(pair.Key, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;

                    var noneVertexPoints = new List<Point3d>();
                    foreach (var point in pair.Value)
                    {
                        var ret = CheckPointIsVertex(curve, point, Tolerance);
                        if(ret != null && !ret.Value)
                            noneVertexPoints.Add(point);
                    }

                    if (noneVertexPoints.Count > 0)
                    {
                        _missingVertexInfos.Add(new MissingVertexInfo()
                        {
                            PolylineId = pair.Key,
                            Positions = noneVertexPoints
                        });
                    }
                }
                transaction.Commit();
            }
        }

        public bool FixAll()
        {
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var missingVertexInfo in MissingVertexInfos)
                {
                    var curve = transaction.GetObject(missingVertexInfo.PolylineId, OpenMode.ForWrite) as Curve;
                    foreach (var position in missingVertexInfo.Positions)
                    {
                        AddVertex.AddVertexFromPolyline(transaction, curve, position);
                    }
                }
                transaction.Commit();
            }
            return true;
        }

        private bool? CheckPointIsVertex(Curve curve, Point3d point, double tolerance)
        {
            Point3d closestPoint = curve.GetClosestPointTo(point, true);
            if (closestPoint != point)
                return null;

            var param = curve.GetParameterAtPoint(closestPoint);
            var paramBase = (int)Math.Floor(param);
            if (param - Math.Floor(param) > Math.Ceiling(param) - param)
            {
                paramBase = (int)Math.Ceiling(param);
            }
            var abs = Math.Abs(paramBase - param);
            return abs < tolerance; // parameter 不是整数
        }
    }
}
