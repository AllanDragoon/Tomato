using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public class PolygonIncludeSearcher : AlgorithmWithDatabase
    {
        private List<KeyValuePair<ObjectId, ObjectId>> _includePolygons =
            new List<KeyValuePair<ObjectId, ObjectId>>();
        public IEnumerable<KeyValuePair<ObjectId, ObjectId>> IncludePolygons
        {
            get { return _includePolygons; }
        }

        public PolygonIncludeSearcher(Database database)
            : base(database)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            var allVertices = new List<CurveVertex>();
            var curveIds = new List<ObjectId>();
            using (var transaction = this.Database.TransactionManager.StartTransaction())
            {
                foreach (var objId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    if (!IsCurveClosed(curve))
                        continue;

                    curveIds.Add(objId);
                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    allVertices.AddRange(vertices.Select(it => new CurveVertex(it, objId)));
                }
                transaction.Commit();
            }
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point, ignoreZ: true);

            // Use kd tree to check include.
            var analyzed = new HashSet<KeyValuePair<ObjectId, ObjectId>>();
            using (var transaction = this.Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in curveIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    var extents = curve.GeometricExtents;
                    var nearVertices = kdTree.BoxedRange(extents.MinPoint, extents.MaxPoint);
                    
                    foreach (var curveVertex in nearVertices)
                    {
                        if (curveVertex.Id == objectId || 
                            analyzed.Contains(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id)))
                        { 
                            continue;
                        }

                        analyzed.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        var targetCurve = transaction.GetObject(curveVertex.Id, OpenMode.ForRead) as Curve;
                        if (IsInclude(curve, targetCurve, transaction))
                        {
                            _includePolygons.Add(new KeyValuePair<ObjectId, ObjectId>(objectId, curveVertex.Id));
                        }
                    }
                }
                transaction.Commit();
            }
        }

        private bool IsInclude(Curve sourceCurve, Curve targetCurve, Transaction transaction)
        {
            var sourceVertices = CurveUtils.GetDistinctVertices(sourceCurve, transaction).ToList();
            var sourceVertices2d = sourceVertices.Select(it => new Point2d(it.X, it.Y)).ToList();
            if (sourceVertices2d[0] != sourceVertices2d[sourceVertices2d.Count - 1])
                sourceVertices2d.Add(sourceVertices2d[0]);
            var sourcePolygon = sourceVertices2d.ToArray();

            var targetVertices = CurveUtils.GetDistinctVertices(targetCurve, transaction).ToList();
            foreach (var targetVertex in targetVertices)
            {
                var point2D = new Point2d(targetVertex.X, targetVertex.Y);
                if (!ComputerGraphics.IsInPolygon(sourcePolygon, point2D, sourcePolygon.Length - 1))
                    return false;
            }

            // There is a case, a polyon's all vertices are on another concave polygon, but the former is not in the latter.
            // So we need to use the former's edge middle points to check.
            if(targetVertices[0] != targetVertices[targetVertices.Count - 1])
                targetVertices.Add(targetVertices[0]);
            var middlePoints = GetAllMiddlePoints(targetVertices);
            foreach (var middlePoint in middlePoints)
            {
                var point2D = new Point2d(middlePoint.X, middlePoint.Y);
                if (!ComputerGraphics.IsInPolygon(sourcePolygon, point2D, sourcePolygon.Length - 1))
                    return false;
            }

            return true;
        }

        private IEnumerable<Point3d> GetAllMiddlePoints(List<Point3d> vertices)
        {
            var result = new List<Point3d>();
            if (vertices.Count < 2)
                return result;
            var previous = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                var current = vertices[i];
                var middlePoint = previous + (current - previous) / 2;
                result.Add(middlePoint);
                previous = current;
            }
            return result;
        }

        private bool IsCurveClosed(Curve curve)
        {
            bool closed = false;
            var polyline = curve as Polyline;
            var polyline2d = curve as Polyline2d;
            if (polyline != null)
                closed = polyline.Closed;
            else if (polyline2d != null)
                closed = polyline2d.Closed;
            return closed;
        }
    }
}
