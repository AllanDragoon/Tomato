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
    public abstract class AlgorithmWithDatabase : AlgorithmBase
    {
        private readonly Database _mDatabase;

        public Database Database {get { return _mDatabase; }}

        public AlgorithmWithDatabase(Database database)
        {
            _mDatabase = database;
        }

        protected IEnumerable<ObjectId> GetAllIdsOfDrawing()
        {
            var result = new List<ObjectId>();
            using (var transaction = _mDatabase.TransactionManager.StartTransaction())
            {
                var modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(_mDatabase);
                var modelSpace = transaction.GetObject(modelSpaceId, OpenMode.ForRead) as BlockTableRecord;
                foreach (ObjectId objectId in modelSpace)
                {
                    result.Add(objectId);
                }
            }
            return result;
        }

        ///// <summary>
        ///// Select entities at point by editor.
        ///// </summary>
        ///// <param name="point"></param>
        ///// <param name="editor"></param>
        ///// <returns></returns>
        //protected ObjectId[] SelectCurvesAtPoint(Point3d point)
        //{
        //    var selectionFilter = SelectionFilterUtils.OnlySelectCurve();
        //    return Editor.SelectEntitiesAtPoint(selectionFilter, point);
        //}
    }
}
