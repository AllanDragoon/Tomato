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
    public abstract class AlgorithmWithEditor : AlgorithmWithDatabase
    {
        public Editor Editor { get; private set; }
        public Document Document { get { return Editor.Document; } }

        public AlgorithmWithEditor(Editor editor) : base(editor.Document.Database)
        {
            Editor = editor;
        }

        /// <summary>
        /// Select entities at point by editor.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="editor"></param>
        /// <returns></returns>
        protected ObjectId[] SelectCurvesAtPoint(Point3d point)
        {
            var selectionFilter = SelectionFilterUtils.OnlySelectCurve();
            return Editor.SelectEntitiesAtPoint(selectionFilter, point);
        }
    }
}
