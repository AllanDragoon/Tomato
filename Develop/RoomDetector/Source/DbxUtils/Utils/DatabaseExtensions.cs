using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace DbxUtils.Utils
{
    public static class DatabaseExtensions
    {
        public static ObjectId GetSymbolTableId(this Database db, Type classType)
        {
            if (classType == typeof(BlockTableRecord))
            {
                return db.BlockTableId;
            }
            if (classType == typeof(DimStyleTableRecord))
            {
                return db.DimStyleTableId;
            }
            if (classType == typeof(LayerTableRecord))
            {
                return db.LayerTableId;
            }
            if (classType == typeof(LinetypeTableRecord))
            {
                return db.LinetypeTableId;
            }
            if (classType == typeof(TextStyleTableRecord))
            {
                return db.TextStyleTableId;
            }
            if (classType == typeof(RegAppTableRecord))
            {
                return db.RegAppTableId;
            }
            if (classType == typeof(UcsTableRecord))
            {
                return db.UcsTableId;
            }
            if (classType == typeof(ViewTableRecord))
            {
                return db.ViewTableId;
            }
            if (classType == typeof(ViewportTableRecord))
            {
                return db.ViewportTableId;
            }
            return default(ObjectId);
        }

        public static ObjectId GetSymbolTableRecId(this Database db, Type classType, string symName)
        {
            var symbolTableId = db.GetSymbolTableId(classType);
            var result = default(ObjectId);
            var transactionManager = db.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var symbolTable = (SymbolTable)transaction.GetObject(symbolTableId, 0);
                if (symbolTable.Has(symName))
                {
                    result = symbolTable[symName];
                }
                transaction.Commit();
            }
            return result;
        }

        public static bool SymbolTableRecExists(this Database db, Type classType, string symName)
        {
            bool result = false;
            var symbolTableId = db.GetSymbolTableId(classType);
            var transactionManager = db.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var symbolTable = (SymbolTable)transaction.GetObject(symbolTableId, 0);
                result = symbolTable.Has(symName);
                transaction.Commit();
            }
            return result;
        }

        public static ObjectId GetOrLoadLinetypeId(this Database db, string lineTypeFileName, string lineTypeName)
        {
            var result = db.GetSymbolTableRecId(typeof(LinetypeTableRecord), lineTypeName);
            if (result.IsNull)
            {
                db.LoadLineTypeFile(lineTypeName, lineTypeFileName);
                result = db.GetSymbolTableRecId(typeof(LinetypeTableRecord), lineTypeName);
                if (!result.IsNull)
                    return result;
                result = db.ContinuousLinetype;
            }
            return result;
        }


        public static ObjectId GetOrLoadLinetypeId(this Database db, string ltypeName)
        {
            var result = db.GetSymbolTableRecId(typeof(LinetypeTableRecord), ltypeName);
            if (result.IsNull)
            {
                var array = new[]
                    {
                        "acad.lin",
                        "acadiso.lin",
                        "ltypeshp.lin"
                    };
                int num = array.Length;
                for (int i = 0; i < num; i++)
                {
                    db.LoadLineTypeFile(ltypeName, array[i]);
                    result = db.GetSymbolTableRecId(typeof(LinetypeTableRecord), ltypeName);
                    if (!result.IsNull)
                        return result;
                }
                // Tools.Editor.WriteMessage(string.Format("\nERROR: Could not load linetype \"{0}\", using CONTINUOUS instead.", ltypeName));
                result = db.ContinuousLinetype;
            }
            return result;
        }

        public static void AddNewSymbolRec(this Database db, SymbolTableRecord newRec)
        {
            var symbolTableId = db.GetSymbolTableId(newRec.GetType());
            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var symbolTable = (SymbolTable)transaction.GetObject(symbolTableId, OpenMode.ForWrite);
                symbolTable.Add(newRec);
                transaction.AddNewlyCreatedDBObject(newRec, true);
                transaction.Commit();
            }
        }

        public static IEnumerable<Entity> GetAllEntitiesInModelSpace(this Database db, Transaction trans, OpenMode openMode)
        {
            return db.GetAllEntitiesInModelSpace(trans, openMode, false);
        }

        public static IEnumerable<Entity> GetAllEntitiesInModelSpace(this Database db, Transaction trans, OpenMode openMode, bool openErased)
        {
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], 0);
            var list = new List<Entity>();
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (Entity)trans.GetObject(current, openMode, openErased);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<T> GetAllEntitiesInModelSpace<T>(this Database db, Transaction trans, OpenMode openMode)
        {
            return db.GetAllEntitiesInModelSpace<T>(trans, openMode, false);
        }

        public static IEnumerable<T> GetAllEntitiesInModelSpace<T>(this Database db, Transaction trans, OpenMode openMode, bool openErased)
        {
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], 0);
            var list = new List<T>();
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var entity = trans.GetObject(current, openMode, openErased) as Entity;
                    if (entity is T)
                    {
                        object obj = entity;
                        list.Add((T)((object)obj));
                    }
                }
            }
            return list;
        }

        public static IEnumerable<Entity> GetAllEntitiesInPaperSpace(this Database db, Transaction trans, OpenMode openMode)
        {
            return db.GetAllEntitiesInPaperSpace(trans, openMode, false);
        }

        public static IEnumerable<Entity> GetAllEntitiesInPaperSpace(this Database db, Transaction trans, OpenMode openMode, bool openErased)
        {
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.PaperSpace], 0);
            var list = new List<Entity>();
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (Entity)trans.GetObject(current, openMode, openErased);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<T> GetAllEntitiesInPaperSpace<T>(this Database db, Transaction trans, OpenMode openMode)
        {
            return db.GetAllEntitiesInPaperSpace<T>(trans, openMode, false);
        }

        public static IEnumerable<T> GetAllEntitiesInPaperSpace<T>(this Database db, Transaction trans, OpenMode openMode, bool openErased)
        {
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.PaperSpace], 0);
            var list = new List<T>();
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var entity = trans.GetObject(current, openMode, openErased) as Entity;
                    if (entity is T)
                    {
                        object obj = entity;
                        list.Add((T)((object)obj));
                    }
                }
            }
            return list;
        }

        public static IEnumerable<LayerTableRecord> GetAllLayers(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<LayerTableRecord>();
            var layerTable = (LayerTable)trans.GetObject(db.LayerTableId, 0);
            using (var enumerator = layerTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (LayerTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static LayerTableRecord FindLayerByName(this Database db, Transaction trans, OpenMode openMode, string layerName)
        {
            var layers = db.GetAllLayers(trans, OpenMode.ForRead);
            LayerTableRecord layer = null;
            foreach (var layerTableRecord in layers)
            {
                // 修改为图层名称大小写不敏感，比如界址点层 JZP/jzp都可以的
                if (layerTableRecord.Name.ToLower() == layerName.ToLower())
                    layer = layerTableRecord;
            }

            return layer;
        }

        public static ObjectIdCollection GetEntitiesByLayerName(this Database db, ObjectId blockReferenceId, string layerName, Type type)
        {
            var ids = new ObjectIdCollection();
            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var layer = db.FindLayerByName(transaction, OpenMode.ForRead, layerName);
                if (layer == null)
                {
                    string message = String.Format("图层{0}不存在", layerName);
                    throw new System.Exception(message);
                }

                var blockReference = transaction.GetObject(blockReferenceId, OpenMode.ForRead) as BlockReference;
                if (blockReference != null)
                {
                    var blockTableRecord = transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (blockTableRecord != null)
                    {
                        using (var enumerator = blockTableRecord.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                var objectId = enumerator.Current;

                                // Check if the object is on the layer
                                var dbObject = transaction.GetObject(objectId, 0);
                                var entity = dbObject as Entity;
                                if (entity != null && entity.LayerId == layer.Id)
                                {
                                    if (type == null)
                                    {
                                        ids.Add(objectId);
                                    }
                                    else if (entity.GetType() == type)
                                    {
                                        ids.Add(objectId);
                                    }
                                }
                            }
                        }
                    }
                }
                
                transaction.Commit();
            }
            return ids;
        }

        public static ObjectIdCollection GetEntitiesByLayerName(this Database db, string layerName, Type type)
        {
            var ids = new ObjectIdCollection();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                ids = GetEntitiesByLayerName(db, tr, layerName, type);
                tr.Commit();
            }
            return ids;
        }

        public static ObjectIdCollection GetEntitiesByLayerName(this Database db, Transaction tr, string layerName, Type type)
        {
            var ids = new ObjectIdCollection();

            var layer = db.FindLayerByName(tr, OpenMode.ForRead, layerName);
            if (layer == null)
                return ids;

            var blockTableRecord = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), 0) as BlockTableRecord;
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var objectId = enumerator.Current;

                    // Check if the object is on the layer
                    var dbObject = tr.GetObject(objectId, 0);
                    var entity = dbObject as Entity;
                    if (entity != null && entity.LayerId == layer.Id)
                    {
                        if (type == null)
                        {
                            ids.Add(objectId);
                        }
                        else if (entity.GetType() == type)
                        {
                            ids.Add(objectId);
                        }
                    }
                }
            }
            return ids;
        }

        public static IEnumerable<BlockTableRecord> GetAllBlocks(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<BlockTableRecord>();
            var blockTableRecord = (BlockTableRecord)trans.GetObject(db.BlockTableId, 0);
            using (var enumerator = blockTableRecord.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (BlockTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<TextStyleTableRecord> GetAllTextStyles(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<TextStyleTableRecord>();
            var textStyleTable = (TextStyleTable)trans.GetObject(db.TextStyleTableId, 0);
            using (var enumerator = textStyleTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (TextStyleTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static string[] GetAllTextStyleNames(this Database db, Transaction trans)
        {
            var list = new List<string>();
            var textStyleTable = (TextStyleTable)trans.GetObject(db.TextStyleTableId, 0);
            using (var enumerator = textStyleTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (TextStyleTableRecord)trans.GetObject(current, OpenMode.ForRead);
                    list.Add(item.Name);
                }
            }
            return list.ToArray();
        }

        public static IEnumerable<LinetypeTableRecord> GetAllLinetypes(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<LinetypeTableRecord>();
            var linetypeTable = (LinetypeTable)trans.GetObject(db.LinetypeTableId, 0);
            using (var enumerator = linetypeTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (LinetypeTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<ViewTableRecord> GetAllViews(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<ViewTableRecord>();
            var viewTable = (ViewTable)trans.GetObject(db.ViewTableId, 0);
            using (var enumerator = viewTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (ViewTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<UcsTableRecord> GetAllUcss(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<UcsTableRecord>();
            var ucsTable = (UcsTable)trans.GetObject(db.UcsTableId, 0);
            using (var enumerator = ucsTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (UcsTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<ViewportTableRecord> GetAllViewports(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<ViewportTableRecord>();
            var viewportTable = (ViewportTable)trans.GetObject(db.UcsTableId, 0);
            using (var enumerator = viewportTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (ViewportTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<RegAppTableRecord> GetAllRegApps(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<RegAppTableRecord>();
            var regAppTable = (RegAppTable)trans.GetObject(db.RegAppTableId, 0);
            using (var enumerator = regAppTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (RegAppTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<DimStyleTableRecord> GetAllDimStyles(this Database db, Transaction trans, OpenMode openMode)
        {
            var list = new List<DimStyleTableRecord>();
            var dimStyleTable = (DimStyleTable)trans.GetObject(db.DimStyleTableId, 0);
            using (var enumerator = dimStyleTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    var item = (DimStyleTableRecord)trans.GetObject(current, openMode);
                    list.Add(item);
                }
            }
            return list;
        }

        public static IEnumerable<BlockReference> GetBlockReferencesByName(this Database db, Transaction trans, OpenMode openMode, string blockName)
        {
            var list = new List<BlockReference>();
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            try
            {
                if (!blockTable.Has(blockName))
                {
                    throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.InvalidBlockName);
                }
                var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[blockName], 0);
                foreach (ObjectId objectId in blockTableRecord.GetBlockReferenceIds(true, false))
                {
                    var item = (BlockReference)trans.GetObject(objectId, 0);
                    list.Add(item);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                throw;
            }
            return list;
        }

        public static IEnumerable<AttributeReference> GetBlockAttributesByName(this Database db, Transaction trans, OpenMode openMode, string blockName, string attributeName)
        {
            var list = new List<AttributeReference>();
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            try
            {
                if (!blockTable.Has(blockName))
                {
                    throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.InvalidBlockName);
                }
                var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[blockName], 0);
                foreach (ObjectId objectId in blockTableRecord.GetBlockReferenceIds(true, false))
                {
                    var blockReference = (BlockReference)trans.GetObject(objectId, 0);
                    foreach (ObjectId objectId2 in blockReference.AttributeCollection)
                    {
                        var attributeReference = (AttributeReference)trans.GetObject(objectId2, openMode);
                        if (attributeReference.Tag == attributeName.ToUpper())
                        {
                            list.Add(attributeReference);
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                throw;
            }
            return list;
        }

        public static IEnumerable<Entity> GetEntitiesInBlock(this Database db, Transaction trans, OpenMode openMode, string blockName)
        {
            var list = new List<Entity>();
            var blockTable = (BlockTable)trans.GetObject(db.BlockTableId, 0);
            try
            {
                if (!blockTable.Has(blockName))
                {
                    throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.InvalidBlockName);
                }
                var blockTableRecord = (BlockTableRecord)trans.GetObject(blockTable[blockName], 0);
                using (var enumerator = blockTableRecord.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        var item = (Entity)trans.GetObject(current, openMode);
                        list.Add(item);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                throw;
            }
            return list;
        }

        public static List<DBObject> GetAllObjectsInModelSpace(this Database db)
        {
            var list = new List<DBObject>();
            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var blockTableRecord = transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), 0) as BlockTableRecord;
                using (var enumerator = blockTableRecord.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        list.Add(transaction.GetObject(current, 0));
                    }
                }
                transaction.Commit();
            }
            return list;
        }

        public static List<DBObject> GetAllObjectsInPaperSpace(Database db)
        {
            var list = new List<DBObject>();
            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var blockTableRecord = transaction.GetObject(SymbolUtilityServices.GetBlockPaperSpaceId(db), 0) as BlockTableRecord;
                using (var enumerator = blockTableRecord.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        list.Add(transaction.GetObject(current, 0));
                    }
                }
                transaction.Commit();
            }
            return list;
        }
    }
}
