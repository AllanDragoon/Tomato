using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// 零长度对象可能是在从其它应用程序输入数据或数字化地图数据时，不经意间引入的。
    /// 使用“零长度对象”可找到有起点和终点但长度为零或缺少一个端点的直线、圆弧和多段线，并删除它们。
    /// </summary>
    public class ZeroLengthEraser : AlgorithmWithEditor
    {
        private double _tolerance = Tolerance.Global.EqualPoint;

        public ZeroLengthEraser(Editor editor) : base(editor)
        {
            _zerolengthObjectIdCollection = new ObjectIdCollection();
        }

        private ObjectIdCollection _zerolengthObjectIdCollection;

        public ObjectIdCollection ZerolengthObjectIdCollection
        {
            get { return _zerolengthObjectIdCollection; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            _zerolengthObjectIdCollection = GetZeroLengthObjectIds(db, selectedObjectIds, _tolerance);
        }

        private static ObjectIdCollection GetZeroLengthObjectIds(Database database, IEnumerable<ObjectId> selectedObjectIds, double tolerance)
        {
            var zeroLengthObjectIds = new ObjectIdCollection();
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in selectedObjectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all specified layers curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        // 判断多段线顶点个数和长度
                        var polyline = curve as Polyline;
                        if (polyline != null)
                        {
                            if (polyline.NumberOfVertices < 2 || polyline.Length.SmallerOrEqual(tolerance))
                                zeroLengthObjectIds.Add(objectId);
                            continue;
                        }

                        // 判断二维多段线顶点个数和长度
                        var polyline2d = curve as Polyline2d;
                        if (polyline2d != null)
                        {
                            int count = 0;
                            foreach (ObjectId vertexId in polyline2d)
                            {
                                ++count;
                                if (count > 1)
                                {
                                    break;
                                }
                            }
                            if (count < 2 || polyline2d.Length.SmallerOrEqual(tolerance))
                                zeroLengthObjectIds.Add(objectId);
                            continue;
                        }

                        // 判断直线长度
                        var line = curve as Line;
                        if (line != null)
                        {
                            if (line.Length.SmallerOrEqual(tolerance))
                                zeroLengthObjectIds.Add(objectId);
                            continue;
                        }

                        // 判断圆弧长度
                        var arc = curve as Arc;
                        if (arc != null)
                        {
                            if (arc.Length.SmallerOrEqual(Tolerance.Global.EqualPoint))
                                zeroLengthObjectIds.Add(objectId);
                            continue;
                        }
                    }
                }

                // Commit() has higher performance than Abort().
                // http://spiderinnet1.typepad.com/blog/2012/01/autocad-net-commit-transaction-or-not-when-reading.html
                // It is clear now that committing transactions is more efficient than aborting them even for reading operations.
                trans.Commit();
            }

            return zeroLengthObjectIds;
        }
    }
}
