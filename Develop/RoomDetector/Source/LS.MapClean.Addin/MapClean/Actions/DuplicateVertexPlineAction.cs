using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.MapClean
{
    public class DuplicateVertexPlineAction : MapCleanActionBase
    {
        public DuplicateVertexPlineAction(Document document)
            : base(document)
        {
            Tolerance = 0.05; // 默认的是0.05
        }

        public override ActionType ActionType
        {
            get { return ActionType.DuplicateVertexPline; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            // Do nothing
            return null;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            // Do nothing
            return Status.NoFixMethod;
        }

        protected override bool CheckAndFixAllImpl(IEnumerable<ObjectId> ids )
        {
            var editor = Document.Editor;
            // Check
            editor.WriteMessage("\n开始检查重复点\n");

            var duplicateVertexPolylineIds = new List<ObjectId>();
            using (var waitCursor = new WaitCursorSwitcher())
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
               foreach (var id in ids)
                {
                    if (HasDuplicateVertices(id, transaction, Tolerance))
                    {
                        duplicateVertexPolylineIds.Add(id);
                    }
                }
               transaction.Commit();
            }

            if (duplicateVertexPolylineIds.Count == 0)
            {
                editor.WriteMessage("\n检测到0个对象，无需修复\n");
                return true;
            }

            // Ask whether to fix
            var message = String.Format("\n检测到{0}个对象存在顶点重复，是否修复?", duplicateVertexPolylineIds.Count);
            if (!AskContinueFix(message, editor))
            {
                return false;
            }

            // 获取所有的三岔点，避免修复重复点的时候删除三岔点
            var crotchPointsMap = CrotchPointSearcher.GetCrotchPoints(Document.Database, ids);
            // 开始修复
            using(var waitCursor = new WaitCursorSwitcher())
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in duplicateVertexPolylineIds)
                {
                    var crotchPoints = new List<Point3d>();
                    if (crotchPointsMap.ContainsKey(id))
                        crotchPoints = crotchPointsMap[id];
                    FixDuplicateVertices(id, Tolerance, transaction, crotchPoints);
                }
                transaction.Commit();
            }
            editor.WriteMessage("\n修复完毕!\n");
            return true;
        }

        protected bool AskContinueFix(string message, Editor editor)
        {
            var options = AcadPromptUtil.CreatePromptOptions<PromptKeywordOptions>(message, new [] { "Yes", "No" },
                new [] { "是", "否" }, new [] { 'Y', 'N' });
            options.AllowNone = true;
            PromptResult promptResult = null;
            do
            {
                promptResult = editor.GetKeywords(options);
            } while (promptResult.Status != PromptStatus.OK
                && promptResult.Status != PromptStatus.Cancel
                && promptResult.Status != PromptStatus.None);

            if (promptResult.Status == PromptStatus.Cancel)
                return false;

            if (promptResult.Status == PromptStatus.OK && promptResult.StringResult == "No")
                return false;

            return true;
        }

        private bool HasDuplicateVertices(ObjectId id, Transaction transaction, double tolerance)
        {
            using (var curve = transaction.GetObject(id, OpenMode.ForRead))
            {
                if (curve is Polyline)
                {
                    return HasDuplicateVertices((Polyline) curve, tolerance);
                }
                if (curve is Polyline2d)
                {
                    return HasDuplicateVertices((Polyline2d) curve, transaction, tolerance);
                }
                return false;
            }
        }

        private bool HasDuplicateVertices(Polyline polyline, double tolerance)
        {
            var numOfVertices = polyline.NumberOfVertices;
            if (polyline.NumberOfVertices <= 1)
                return false;

            var sqrTol = tolerance*tolerance;
            for (int i = 0; i < numOfVertices; i++)
            {
                var currentIndex = i%numOfVertices;
                var nextIndex = (i + 1)%numOfVertices;
                var currentPoint = polyline.GetPoint3dAt(currentIndex);
                var nextPoint = polyline.GetPoint3dAt(nextIndex);
                var sqrLength = (nextPoint - currentPoint).LengthSqrd;
                if (sqrLength.SmallerOrEqual(sqrTol))
                    return true;
            }
            return false;
        }

        private bool HasDuplicateVertices(Polyline2d polyline2D, Transaction transaction, double tolerance)
        {
            var sqrTol = tolerance*tolerance;
            var vertices = new List<Point3d>();
            foreach (var vertexId in polyline2D)
            {
                Vertex2d vertex = null;
                if (vertexId is ObjectId)
                {
                    var id = (ObjectId)vertexId;
                    // 二维多段线，如果顶点已经删除，但是二维多段线依然保留他们的信息，
                    // 除非重新关闭dwg，然后再打开dwg。
                    if (id.IsErased)
                        continue;

                    if (id.IsValid)
                        vertex = transaction.GetObject((ObjectId)vertexId, OpenMode.ForRead) as Vertex2d;
                }
                else if (vertexId is Vertex2d)
                    vertex = (Vertex2d)vertexId;

                if (vertex == null)
                    continue;

                var point = vertex.Position;
                // TEMP：发现有的二维多段线的Z值并不是0.0，但是无法找到原因，临时改成0.0
                if (!point.Z.EqualsWithTolerance(0.0))
                    point = new Point3d(point.X, point.Y, 0.0);

                vertices.Add(point);
            }

            var count = vertices.Count;
            if (count <= 1)
                return false;
            for (int i = 0; i < count; i++)
            {
                var currentIndex = i%count;
                var nextIndex = (i + 1)%count;
                var currentPoint = vertices[currentIndex];
                var nextPoint = vertices[nextIndex];
                var sqrLength = (nextPoint - currentPoint).LengthSqrd;
                if (sqrLength.SmallerOrEqual(sqrTol))
                    return true;
            }
            return false;
        }

        private bool FixDuplicateVertices(ObjectId id, double tolerance, Transaction transaction, List<Point3d> crotchPoints)
        {
            using (var curve = transaction.GetObject(id, OpenMode.ForWrite))
            {
                try
                {
                    if (curve is Polyline)
                    {
                        return FixDuplicateVertices((Polyline)curve, tolerance, transaction, crotchPoints);
                    }
                    if (curve is Polyline2d)
                    {
                        return FixDuplicateVertices((Polyline2d)curve, tolerance, transaction, crotchPoints);
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    // 如果多段线只有最后一个点，继续删除点，就会出现eDegenerateGeometry的异常错误了
                    // 这个时候，可以直接删除多段线了。
                    if (ex.Message == "eDegenerateGeometry")
                    {
                        curve.Erase();
                    }
                }
                
                return false;
            }
        }

        private bool FixDuplicateVertices(Polyline polyline, double tolerance, Transaction transaction, List<Point3d> crotchPoints)
        {
            if (polyline.NumberOfVertices == 1)
                return true;

            var sqrTol = tolerance*tolerance;
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                // 如果只有最后一个点，就别删除点了，
                // 需要的话，可以直接删除多段线了，
                // 否则就会出现eDegenerateGeometry的异常错误了。
                //if (polyline.NumberOfVertices == 1)
                //    return true;

                var currentIndex = i%polyline.NumberOfVertices;
                var nextIndex = (i + 1)%polyline.NumberOfVertices;
                var currentPoint = polyline.GetPoint3dAt(currentIndex);
                var nextPoint = polyline.GetPoint3dAt(nextIndex);

                var sqrDistance = (nextPoint - currentPoint).LengthSqrd;
                if (!double.IsNaN(sqrDistance) && sqrDistance.Larger(sqrTol))
                    continue;

                // 如果是三岔点，就不要删除下一个点
                var isCurrentCrotch = crotchPoints.Contains(currentPoint);
                var isNextCrotch = crotchPoints.Contains(nextPoint);
                if (isNextCrotch && !isCurrentCrotch)
                {
                    polyline.RemoveVertexAt(currentIndex);
                }
                else
                {
                    polyline.RemoveVertexAt(nextIndex);
                }
                i--;
            }
            return true;
        }

        private bool FixDuplicateVertices(Polyline2d polyline2d, double tolerance, 
            Transaction transaction, List<Point3d> crotchPoints)
        {
            var vertices = new List<object>();
            foreach (var vertexId in polyline2d)
            {
                vertices.Add(vertexId);
            }

            if (vertices.Count == 1)
                return true;

            var sqrTol = tolerance*tolerance;
            for (int i = 0; i < vertices.Count; i++)
            {
                var currentIndex = i%vertices.Count;
                var nextIndex = (i + 1)%vertices.Count;
                Vertex2d currentVertex = null;
                Vertex2d nextVertex = null;
                if (vertices[currentIndex] is ObjectId)
                {
                    currentVertex = (Vertex2d)transaction.GetObject((ObjectId)vertices[currentIndex], OpenMode.ForWrite);
                }
                else
                {
                    currentVertex = (Vertex2d) vertices[currentIndex];
                }

                if (vertices[nextIndex] is ObjectId)
                {
                    nextVertex = (Vertex2d) transaction.GetObject((ObjectId) vertices[nextIndex], OpenMode.ForWrite);
                }
                else
                {
                    nextVertex = (Vertex2d) vertices[nextIndex];
                }

                var currentPoint = currentVertex.Position;
                var nextPoint = nextVertex.Position;
                var sqrLength = (nextPoint - currentPoint).LengthSqrd;
                if (!double.IsNaN(sqrLength) && sqrLength.Larger(sqrTol))
                    continue;

                // 如果是三岔点，就不要删除下一个点
                var isCurrentCrotch = crotchPoints.Contains(currentPoint);
                var isNextCrotch = crotchPoints.Contains(nextPoint);
                if (isNextCrotch && !isCurrentCrotch)
                {
                    nextVertex.Erase();
                    vertices.RemoveAt(nextIndex);
                }
                else
                {
                    currentVertex.Erase();
                    vertices.RemoveAt(currentIndex);
                }
                i--;
            }
            return true;
        }
    }
}
