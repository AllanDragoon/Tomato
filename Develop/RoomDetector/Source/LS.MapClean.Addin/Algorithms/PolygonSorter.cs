using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace LS.MapClean.Addin.Algorithms
{
    public class PolygonSorter : AlgorithmWithDatabase
    {
        private IEnumerable<ObjectId> _sortedPolygonIds = new List<ObjectId>();
        public IEnumerable<ObjectId> SortedPolygonIds
        {
            get { return _sortedPolygonIds; }
        }

        public PolygonSorter(Database database)
            : base(database)
        {
        }

        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            throw new NotImplementedException();
        }
    }
}
