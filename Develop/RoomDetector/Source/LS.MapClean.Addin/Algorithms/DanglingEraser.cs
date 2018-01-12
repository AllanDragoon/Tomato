using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using QuickGraph;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// http://knowledge.autodesk.com/support/autocad-map-3d/learn-explore/caas/documentation/MAP/2014/ENU/filesMAPUSE/GUID-AF898D46-9D28-40AA-9AB4-C5A57F59FBD2-htm.html
    /// Use Erase Dangling Objects to locate an object with at least one end point that is not shared by another object, and erase the object.
    /// </summary>
    public class DanglingEraser : AlgorithmWithEditor
    {
        private double _tolerance = 0.5;

        private IEnumerable<IEnumerable<SEdge<CurveVertex>>> _danglingPaths;
        public IEnumerable<IEnumerable<SEdge<CurveVertex>>> DanglingPaths
        {
            get { return _danglingPaths; }
        }

        // The Erase Dangling Objects action searches for and deletes all line, arc, and polyline dangling edges, and nodes. 
        // Dangling objects do not include closed polylines.
        public DanglingEraser(Editor editor, double tolerance)
            : base(editor)
        {
            _tolerance = tolerance;
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            // The Erase Dangling Objects action searches for and deletes all line, arc, and polyline dangling edges, and nodes. 
            // NOTE: we need to take account of a chain of dangling objects, such as line A connects to line B, 
            // but line B connects to nothing, then line B will be erased, and line A will be erased together.
            //
            // |\
            // | \
            // |__\_________A________B
            //
            if (!selectedObjectIds.Any())
                return;

            var watch = Stopwatch.StartNew();
            var database = Editor.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                //// Build a quick graph by using CurveGraphBuilder
                //var graphBuilder = new CurveGraphBuilder(objIds, transaction);
                //graphBuilder.SelectCurves = SelectCurvesAtPoint;
                //graphBuilder.BuildGraph();

                //// Search dangling paths.
                //_danglingPaths = graphBuilder.SearchDanglingPaths();

                // Build a quick graph by using CurveGraphBuilder
                var graphBuilderKdTree = new KdTreeCurveGraphBuilder(selectedObjectIds, transaction);
                graphBuilderKdTree.BuildGraph();

                // Search dangling paths.
                var danglingPathList = graphBuilderKdTree.SearchDanglingPaths().ToList();

                // Loop for check each dangling path length is larger than tolerance. If larger, we will filter it out.
                for (int i = 0; i < danglingPathList.Count; i++)
                {
                    var danglingLength = 0.0;
                    foreach (SEdge<CurveVertex> sEdge in danglingPathList.ElementAt(i))
                    {
                        danglingLength += (sEdge.Source.Point - sEdge.Target.Point).Length;
                    }

                    // remove the item which dangling length is larger than tolerance.
                    if (danglingLength > _tolerance)
                        danglingPathList.Remove(danglingPathList.ElementAt(i));
                }

                _danglingPaths = danglingPathList;

                transaction.Commit();
            }
            // the code that you want to measure comes here
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Editor.WriteMessage("\n查找悬挂对象花费时间{0}毫秒", elapsedMs);
        }

        public void Fix()
        {

        }

        public IEnumerable<ObjectId> GetDanglingIds()
        {
            if (_danglingPaths == null)
                return new ObjectId[0];
            var result = new HashSet<ObjectId>();
            foreach (var danglingPath in _danglingPaths)
            {
                foreach (var edge in danglingPath)
                {
                    if (edge.Source.Id != edge.Target.Id)
                        continue;

                    result.Add(edge.Source.Id);
                }
            }
            return result;
        }
    }
}
