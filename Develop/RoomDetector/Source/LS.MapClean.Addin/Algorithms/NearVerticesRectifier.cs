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
    public class NearVerticesRectifier : AlgorithmWithDatabase
    {
        private double _tolerance = 0.00005;
        private List<List<CurveVertex>> _nearVertices = new List<List<CurveVertex>>();
        public List<List<CurveVertex>> NearVertices
        {
            get { return _nearVertices; }
        }

        public NearVerticesRectifier(Database database, double tolerance)
            : base(database)
        {
            _tolerance = tolerance;
        }
        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            if (!selectedObjectIds.Any())
                return;

            // 1. Create a kd tree 
            var database = Database;
            var allVertices = new List<CurveVertex>();
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in selectedObjectIds)
                {
                    var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve == null)
                        continue;
                    var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
                    allVertices.AddRange(vertices.Select(it => new CurveVertex(it, objectId)));
                }
                transaction.Commit();
            }
            var kdTree = new CurveVertexKdTree<CurveVertex>(allVertices, it => it.Point.ToArray(), ignoreZ: true);
            
            // 3. Filter out points that on same curve
            var visited = new HashSet<CurveVertex>();
            using (var tolerance = new SafeToleranceOverride(1E-5, 1E-5))
            foreach (var curveVertex in allVertices)
            {
                if (visited.Contains(curveVertex))
                    continue;

                var nears = kdTree.NearestNeighbours(curveVertex.Point.ToArray(), _tolerance);
                var qualified = new List<CurveVertex>();
                qualified.Add(curveVertex);
                foreach (var near in nears)
                {
                    visited.Add(near);
                    if (near.Point == curveVertex.Point)
                        continue;
                    qualified.Add(near);
                }
                if(qualified.Count > 1)
                    _nearVertices.Add(qualified);
            }
        }

        public static void RectifyNearVertices(IEnumerable<CurveVertex> vertices, Transaction transaction)
        {
            Point3d? position = null;
            foreach (var curveVertex in vertices)
            {
                if (position == null)
                {
                    position = curveVertex.Point;
                    continue;
                }

                using (var curve = transaction.GetObject(curveVertex.Id, OpenMode.ForRead) as Curve)
                {
                    if (curve == null)
                        continue;

                    curve.UpgradeOpen();
                    var line = curve as Line;
                    var polyline = curve as Polyline;
                    var polyline2d = curve as Polyline2d;

                    if (line != null)
                    {
                        RectifyVertex(line, curveVertex.Point, position.Value);
                    }
                    else if (polyline != null)
                    {
                        RectifyVertex(polyline, curveVertex.Point, position.Value);
                    }
                    else if (polyline2d != null)
                    {
                        RectifyVertex(polyline2d, curveVertex.Point, position.Value, transaction);
                    }
                }
            }
        }

        private static void RectifyVertex(Line line, Point3d linePoint, Point3d targetPoint)
        {
            if (line.StartPoint == linePoint)
                line.StartPoint = targetPoint;
            if (line.EndPoint == linePoint)
                line.EndPoint = targetPoint;
        }

        private static void RectifyVertex(Polyline polyline, Point3d point, Point3d targetPoint)
        {
            var num = polyline.NumberOfVertices;
            for (int i = 0; i < num; i++)
            {
                var vertex = polyline.GetPoint3dAt(i);
                if (vertex == point)
                {
                    polyline.SetPointAt(i, new Point2d(targetPoint.X, targetPoint.Y));
                }
            }
        }

        private static void RectifyVertex(Polyline2d polyline2d, Point3d point, Point3d targetPoint, Transaction transaction)
        {
            foreach (var vertexInfo in polyline2d)
            {
                if (vertexInfo is ObjectId)
                {
                    var vertex2d = transaction.GetObject((ObjectId) vertexInfo, OpenMode.ForWrite) as Vertex2d;
                    if (vertex2d != null)
                    {
                        if(vertex2d.Position == point)
                            vertex2d.Position = targetPoint;
                        vertex2d.Dispose();
                    }
                }
                else if (vertexInfo is Vertex2d)
                {
                    ((Vertex2d) vertexInfo).Position = targetPoint;
                }
            }
        }
    }
}
