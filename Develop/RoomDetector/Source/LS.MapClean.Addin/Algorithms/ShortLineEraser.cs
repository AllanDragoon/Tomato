using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    class ShortLineEraser : AlgorithmWithEditor
    {
        private double _tolerance = 0.1;

        public ShortLineEraser(Editor editor, double tolerance) : base(editor)
        {
            _shortLineObjectIdCollection = new ObjectIdCollection();
            _tolerance = tolerance;
        }

        private ObjectIdCollection _shortLineObjectIdCollection;

        public ObjectIdCollection ShortLineObjectIdCollection
        {
            get { return _shortLineObjectIdCollection; }
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            _shortLineObjectIdCollection = GetShortCurves(db, _tolerance, selectedObjectIds);
        }

        private ObjectIdCollection GetShortCurves(Database database, double tolerance, IEnumerable<ObjectId> selectedObjectIds)
        {
            var shortCurveIds = new ObjectIdCollection();

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in selectedObjectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    // Get all curves from modelspace.
                    var curve = trans.GetObject(objectId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        // Get the curve length. Only support Polyline, Polyline2d, Line, Arc.
                        var length = GetCurveLength(curve);
                        if (length == null || length.Value.Larger(tolerance))
                            continue;

                        // Add the short line ObjectId to the collection.
                        shortCurveIds.Add(objectId);
                    }
                }

                // Commit() has higher performance than Abort().
                // http://spiderinnet1.typepad.com/blog/2012/01/autocad-net-commit-transaction-or-not-when-reading.html
                // It is clear now that committing transactions is more efficient than aborting them even for reading operations.
                trans.Commit();
            }

            return shortCurveIds;
        }

        /// <summary>
        /// Only support Polyline, Polyline2d, Line, Arc.
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        private double? GetCurveLength(Curve curve)
        {
            var polyline = curve as Polyline;
            if (polyline != null)
                return polyline.Length;

            var polyline2d = curve as Polyline2d;
            if (polyline2d != null)
                return polyline2d.Length;

            var line = curve as Line;
            if (line != null)
                return line.Length;

            var arc = curve as Arc;
            if (arc != null)
                return arc.Length;

            // NOT IMPL.
            return null;
        }

    }
}
