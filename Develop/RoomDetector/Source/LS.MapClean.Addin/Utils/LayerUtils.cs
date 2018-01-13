using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LS.MapClean.Addin.Utils
{
    public static class LayerUtils
    {
        public static void AddNewWhiteLayer(Database database, string layerName, short layerColor)
        {
            if (HasLayer(database, layerName))
            {
                UpdateLayerColor(database, layerName, layerColor);
            }
            else
            {
                AddNewLayer(database, layerName, "Continuous", layerColor);
            }
        }

        public static ObjectId AddNewLayer(Database database, string layerName, string lineTypeName, short layerColor)
        {
            var color = Color.FromColorIndex(ColorMethod.ByLayer, layerColor);
            return AddNewLayer(database, layerName, lineTypeName, color);
        }

        public static void UpdateLayerColor(Database database, string layerName, short layerColor)
        {
            var color = Color.FromColorIndex(ColorMethod.ByLayer, layerColor);

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForWrite);
                    if (layerTable.Has(layerName))
                    {
                        var objectId = layerTable[layerName];
                        var layerTableRecord = (LayerTableRecord)transaction.GetObject(objectId, OpenMode.ForWrite);
                        layerTableRecord.Color = color;
                    }
                }
                finally
                {
                    transaction.Commit();
                }
            }
        }

        public static ObjectId AddNewLayer(Database database, string layerName, string lineTypeName, Color layerColor)
        {
            var editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            ObjectId result;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                    var linetypeTable = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
                    ObjectId linetypeObjectId;
                    if (linetypeTable.Has(lineTypeName))
                    {
                        linetypeObjectId = linetypeTable[lineTypeName];
                    }
                    else
                    {
                        linetypeObjectId = linetypeTable["Continuous"];
                        editor.WriteMessage("\n注意：线型\"" + lineTypeName + "\"不存在!按 Continuous 线型处理！");
                    }
                    ObjectId objectId;
                    if (layerTable.Has(layerName))
                    {
                        objectId = layerTable[layerName];
                    }
                    else
                    {
                        layerTable.UpgradeOpen();
                        var layerTableRecord = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = layerColor,
                            LinetypeObjectId = linetypeObjectId
                        };
                        objectId = layerTable.Add(layerTableRecord);
                        transaction.AddNewlyCreatedDBObject(layerTableRecord, true);
                        layerTable.DowngradeOpen();
                    }
                    result = objectId;
                }
                finally
                {
                    transaction.Commit();
                }
            }
            return result;
        }

        public static IList<string> GetAllLayers(Database database)
        {
            var result = new List<string>();
            var editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForWrite);

                    foreach (ObjectId id in layerTable)
                    {
                        var symbol = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForRead);
                        result.Add(symbol.Name);
                    }
                }
                finally
                {
                    transaction.Commit();
                    //transaction.Dispose();
                }
            }
            return result;
        }

        public static string GetLayerName(ObjectId objectId)
        {
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                var dbObject = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
                if (dbObject != null)
                {
                    return dbObject.Layer;
                }
            }

            return String.Empty;
        }

        public static ObjectId AddNewLinetype(Database database, string linetypeName, string linetypeFileName)
        {
            var objectId = ObjectId.Null;
            ObjectId result;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var linetypeTable = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForWrite);
                if (linetypeTable.Has(linetypeName))
                {
                    objectId = linetypeTable[linetypeName];
                }
                else
                {
                    try
                    {
                        database.LoadLineTypeFile(linetypeName, linetypeFileName);
                    }
                    catch (System.Exception ex)
                    {
                        var editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                        editor.WriteMessage(ex.Message);
                        result = linetypeTable["Continuous"];
                        return result;
                    }
                }
                transaction.Commit();
                //transaction.Dispose();
                result = objectId;
            }
            return result;
        }

        /// <summary>
        /// Get current layer color.
        /// </summary>
        /// <param name="doc">Current document</param>
        /// <returns>layer color</returns>
        public static EntityColor GetCurrentLayerColor(Database db)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");

            using (Transaction tran = db.TransactionManager.StartTransaction())
            {
                var existedLayerTableRecord = (LayerTableRecord)tran.GetObject(db.Clayer, OpenMode.ForRead, false);
                EntityColor entityColor = existedLayerTableRecord.Color.EntityColor;
                tran.Commit();

                return entityColor;
            }
        }

        public static bool IsAnyEntityOnLockedLayer(IEnumerable<ObjectId> entities)
        {
            if (!entities.Any())
                return false;

            using (var tran = entities.First().Database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in entities)
                {
                    var entity = tran.GetObject(objectId, OpenMode.ForRead) as Entity;
                    var existedLayerTableRecord = (LayerTableRecord) entity.LayerId.GetObject(OpenMode.ForRead);
                    if (existedLayerTableRecord.IsLocked)
                        return true;
                }
            }
            return false;
        }

        public static ObjectId GetLayerByName(Database db, string layerName)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");
            if (string.IsNullOrEmpty(layerName)) throw new ArgumentNullException(/*MSG0*/"layerName");

            ObjectId layerId = ObjectId.Null;
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                // Get layer table from database.
                var layerTable = (LayerTable)transaction.GetObject(db.LayerTableId, OpenMode.ForWrite);

                // Check if the layerName exists.
                if (layerTable.Has(layerName))
                    layerId = layerTable[layerName];

                transaction.Commit();
            }

            return layerId;
        }

        public static ObjectId GetOrCreateLayerByName(Database db, string layerName)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");
            if (string.IsNullOrEmpty(layerName)) throw new ArgumentNullException(/*MSG0*/"layerName");

            ObjectId newLayerId;
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                //Get layer table from database.
                var layerTable = (LayerTable)transaction.GetObject(db.LayerTableId, OpenMode.ForWrite);
                //Check if the layerName is exist. If exist, just return the name for it; else, add the new layer to layer table.
                if (!layerTable.Has(layerName))
                {
                    LayerTableRecord layerTableRecord = new LayerTableRecord();
                    layerTableRecord.Name = layerName;
                    layerTable.UpgradeOpen();
                    newLayerId = layerTable.Add(layerTableRecord);
                    transaction.AddNewlyCreatedDBObject(layerTableRecord, true);
                }
                else
                {
                    newLayerId = layerTable[layerName];
                }
                transaction.Commit();
            }

            return newLayerId;
        }

        public static bool HasLayer(Database db, string layerName)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");
            if (string.IsNullOrEmpty(layerName)) throw new ArgumentNullException(/*MSG0*/"layerName");

            bool bRet = false;
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                //Get layer table from database.
                var layerTable = (LayerTable)transaction.GetObject(db.LayerTableId, OpenMode.ForRead);

                //Check if the layerName is exist. 
                bRet = layerTable.Has(layerName);

                transaction.Commit();
            }

            return bRet;
        }

        /// <summary>
        /// Get layer ObjectId by handle string.(hexadecimal string)
        /// </summary>
        /// <param name="db"></param>
        /// <param name="layerHandle"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static ObjectId GetLayerByHandle(Database db, string layerHandle)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");
            if (string.IsNullOrEmpty(layerHandle)) throw new ArgumentNullException(/*MSG0*/"layerHandle");

            //Try to parse the handle string to long.
            long handleValue;
            try
            {
                //Convert hexadecimal string to long.
                handleValue = Convert.ToInt64(layerHandle, 16);
            }
            catch
            {
                //Convert handle to long failed. Return ObjectId.Null
                return ObjectId.Null;
            }

            ObjectId layerId = ObjectId.Null;
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                Handle handle = new Handle(handleValue);
                //Try to get the handle object id by handle.
                bool result = db.TryGetObjectId(handle, out layerId);
                if (!result)
                    return ObjectId.Null;

                //Make sure the object of layerId is a LayerTableRecord.
                var layerTableRecord = transaction.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                if (layerTableRecord == null)
                    return ObjectId.Null;

                transaction.Commit();
            }

            return layerId;
        }

        /// <summary>
        /// unlock all the layers in the database.
        /// </summary>
        /// <param name="database">current database.</param>
        /// <param name="unlockedLayerIds">out unlocked layer id collection.</param>
        public static void UnlockAllLayers(Database database, out List<ObjectId> unlockedLayerIds)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");
            unlockedLayerIds = new List<ObjectId>();

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)trans.GetObject(database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId ltrId in layerTable)
                {
                    var layerTableRecord = (LayerTableRecord)trans.GetObject(ltrId, OpenMode.ForWrite);
                    if (layerTableRecord != null && layerTableRecord.IsLocked)
                    {
                        layerTableRecord.IsLocked = false;
                        unlockedLayerIds.Add(layerTableRecord.ObjectId);
                    }
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// Lock specified layers by layer ids.
        /// </summary>
        /// <param name="database">current database.</param>
        /// <param name="layerIds">layer id collections.</param>
        public static void LockLayers(Database database, IEnumerable<ObjectId> layerIds)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");
            if (layerIds == null) throw new ArgumentNullException(/*MSG0*/"layerIds");

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId layerId in layerIds)
                {
                    var layerTableRecord = (LayerTableRecord)trans.GetObject(layerId, OpenMode.ForWrite);
                    if (layerTableRecord != null && !layerTableRecord.IsLocked)
                        layerTableRecord.IsLocked = true;
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// Get current layer name.
        /// </summary>
        /// <param name="database">current database.</param>
        public static string GetCurrentLayerName(Database database)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

            using (Transaction tran = database.TransactionManager.StartTransaction())
            {
                var existedLayerTableRecord = (LayerTableRecord)tran.GetObject(database.Clayer, OpenMode.ForRead, false);
                string name = existedLayerTableRecord.Name;
                tran.Commit();
                return name;
            }
        }

        /// <summary>
        /// Set current layer name.
        /// </summary>
        /// <param name="database">current database.</param>
        /// <param name="layerName">Layer Name</param>
        public static void SetCurrentLayerName(Database database, string layerName)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

            using (Transaction tran = database.TransactionManager.StartTransaction())
            {
                var existedLayerTableRecord = (LayerTableRecord)tran.GetObject(database.Clayer, OpenMode.ForWrite, false);
                existedLayerTableRecord.Name = layerName;
                tran.Commit();
            }
        }

        public static ObjectId GetLinetype(Database db, string linetypeName)
        {
            if (db == null) throw new ArgumentNullException(/*MSG0*/"db");
            if (string.IsNullOrEmpty(linetypeName)) throw new ArgumentNullException(/*MSG0*/"linetypeName");

            ObjectId linetypeId = ObjectId.Null; ;
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                //Get linetype table from database.
                var linetypeTable = (LinetypeTable)transaction.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                //Check if the linetypeName is exist. 
                if (linetypeTable.Has(linetypeName))
                    linetypeId = linetypeTable[linetypeName];

                transaction.Commit();
            }
            return linetypeId;
        }

        public static DBObjectCollection GetCurrentDatabaseLayerEntities(string layerKey)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var entities = new DBObjectCollection();

            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);

                if (!lt.Has(layerKey))
                    return entities;

                var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in modelSpace)
                {
                    // if the id is not a valid,ignore.
                    if (!id.IsValid)
                        continue;

                    var entity = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null)
                        continue;
                    if (entity.Layer == layerKey)
                    {
                        entities.Add(entity);
                    }
                }

                trans.Commit();
            }

            return entities;
        }

        /// <summary>
        /// Get all entities from specified layers.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="layerNames">layer names will be splited by comma.</param>
        /// <returns></returns>
        public static IEnumerable<ObjectId> GetObjectIdcollectionFromLayerNames(Database database, string layerNames)
        {
            var layersObjectIds = new List<ObjectId>();
            // Get all layer names from layer names text. If layerNames is "*" or empty string, that means all layers has been selected.
            bool isSelectedAllLayers = (layerNames == "*" || layerNames == string.Empty);
            string[] layerNameCollection = null;
            if(!isSelectedAllLayers)
                layerNameCollection = layerNames.Split(',');

            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)trans.GetObject(database.LayerTableId, OpenMode.ForRead);
                var layers = new List<ObjectId>();
                if (isSelectedAllLayers)
                {
                    foreach (ObjectId layerId in lt)
                    {
                        var layerTableRecord = (LayerTableRecord)trans.GetObject(layerId, OpenMode.ForRead);
                        // If layer is off, continue.
                        if (layerTableRecord.IsOff || layerTableRecord.IsFrozen)
                            continue;
                        layers.Add(layerTableRecord.Id);
                    }
                }
                else
                {
                    foreach (var name in layerNameCollection)
                    {
                        var layerTableRecord = (LayerTableRecord)trans.GetObject(lt[name], OpenMode.ForRead);
                        // If layer is off, continue.
                        if (layerTableRecord.IsOff || layerTableRecord.IsFrozen)
                            continue;
                        layers.Add(layerTableRecord.Id);
                    }
                }

                var blockTable = (BlockTable)trans.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in modelSpace)
                {
                    // if the id is not a valid,ignore.
                    if (!id.IsValid)
                        continue;

                    var entity = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null)
                        continue;

                    // Check if we select all layer entities or the entity layer is in layerNameCollection.
                    // If so, we will add it to our collection.
                    if (layers.Contains(entity.LayerId))
                    {
                        layersObjectIds.Add(id);
                    }
                }

                trans.Commit();
            }

            return layersObjectIds;
        }

    }
}
