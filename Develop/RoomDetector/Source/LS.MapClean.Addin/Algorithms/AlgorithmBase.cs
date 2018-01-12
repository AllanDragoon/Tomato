using System.Collections.Generic;
using System.Windows.Documents;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Algorithms
{
    public abstract class AlgorithmBase
    {
        public abstract void Check(IEnumerable<ObjectId> selectedObjectIds);
    }
}
