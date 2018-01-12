using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using LS.MapClean.Addin.MapClean;

namespace LS.MapClean.Addin.Settings
{
    public class MapCleanSetting
    {
        private List<MapCleanActionBase> m_mapCleanActions = new List<MapCleanActionBase>(); 
        private ObjectIdCollection m_mapCleanObjectIds = new ObjectIdCollection();

        public List<MapCleanActionBase> MapCleanActions
        {
            get { return m_mapCleanActions; }
        }

        public ObjectIdCollection MapCleanObjectIdCollection
        {
            get { return m_mapCleanObjectIds; }
        }
    }
}
