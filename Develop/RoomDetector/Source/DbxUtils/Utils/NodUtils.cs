using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace DbxUtils.Utils
{
    public static class NodUtils
    {
        private const string FishingBaitDictName = "FV_FISHINGBAIT";
        /// <summary>
        /// Get database's dictionary id.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="dictName"></param>
        /// <param name="createIfNotExisting"></param>
        /// <returns></returns>
        public static ObjectId GetNodDictionaryId(Database database, string dictName, bool createIfNotExisting)
        {
            var dictId = ObjectId.Null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(dictName) && createIfNotExisting)
                {
                    nod.UpgradeOpen();
                    var dict = new DBDictionary();
                    dictId = nod.SetAt(dictName, dict);
                    transaction.AddNewlyCreatedDBObject(dict, add:true);
                }
                else
                {
                    dictId = nod.GetAt(dictName);
                }
                transaction.Commit();
            }
            return dictId;
        }

        public static TypedValue[] GetNodRecordValue(Database database, string dictName, string recordName)
        {
            ResultBuffer rb = null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var dictId = GetNodDictionaryId(database, dictName, createIfNotExisting: true);
                var dict = (DBDictionary) transaction.GetObject(dictId, OpenMode.ForRead);
                if (dict.Contains(recordName))
                {
                    var recordId = dict.GetAt(recordName);
                    var record = transaction.GetObject(recordId, OpenMode.ForRead) as Xrecord;
                    if (record != null)
                        rb = record.Data;
                }
                transaction.Commit();
            }

            if(rb == null)
                return new TypedValue[0];
            return rb.AsArray();
        }

        public static ObjectId SetNodRecordValue(Database database, string dictName, string recordName, TypedValue[] values)
        {
            var recordId = ObjectId.Null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var dictId = GetNodDictionaryId(database, dictName, createIfNotExisting: true);
                var dict = (DBDictionary)transaction.GetObject(dictId, OpenMode.ForWrite);
                if (dict.Contains(recordName))
                {
                    var oldRecordId = dict.GetAt(recordName);
                    var record = transaction.GetObject(oldRecordId, OpenMode.ForWrite);
                    record.Erase();
                }
                var newRecord = new Xrecord();
                newRecord.Data = new ResultBuffer(values);
                recordId = dict.SetAt(recordName, newRecord);
                transaction.AddNewlyCreatedDBObject(newRecord, add:true);
                transaction.Commit();
            }
            return recordId;
        }

        public static ObjectId GetNodFishingBaitDictioaryId(Database database, bool createIfNotExisting)
        {
            return GetNodDictionaryId(database, FishingBaitDictName, createIfNotExisting);
        }

        public static TypedValue[] GetNodFishingBaitRecordValue(Database database, string recordName)
        {
            return GetNodRecordValue(database, FishingBaitDictName, recordName);
        }

        public static ObjectId SetNodFishingBaitRecordValue(Database database, string recordName, TypedValue[] values)
        {
            return SetNodRecordValue(database, FishingBaitDictName, recordName, values);
        }


        /// <summary>
        /// Retrieve or create an Entry with the given name into the Extension Dictionary of the passed-in object.
        /// </summary>
        /// <param name="id">The object hosting the Extension Dictionary</param>
        /// <param name="entryName">The name of the dictionary entry to get or set</param>
        /// <returns>The ObjectId of the diction entry, old or new</returns>
        public static ObjectId GetSetExtensionDictionaryEntry(ObjectId id, string entryName)
        {
            ObjectId ret = ObjectId.Null;
            using (var tr = id.Database.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (obj.ExtensionDictionary == ObjectId.Null)
                {
                    obj.UpgradeOpen();
                    obj.CreateExtensionDictionary();
                    obj.DowngradeOpen();
                }

                var dict = (DBDictionary)tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
                if (!dict.Contains(entryName))
                {
                    var xRecord = new Xrecord();
                    dict.UpgradeOpen();
                    dict.SetAt(entryName, xRecord);
                    tr.AddNewlyCreatedDBObject(xRecord, true);

                    ret = xRecord.ObjectId;
                }
                else
                    ret = dict.GetAt(entryName);

                tr.Commit();
            }

            return ret;
        }

        /// <summary>
        /// Remove the named Dictionary Entry from the passed-in object if applicable.
        /// </summary>
        /// <param name="id">The object hosting the Extension Dictionary</param>
        /// <param name="entryName">The name of the Dictionary Entry to remove</param>
        /// <returns>True if really removed, false if not there at all</returns>
        public static bool RemoveExtensionDictionaryEntry(ObjectId id, string entryName)
        {
            bool ret = false;

            using (var tr = id.Database.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (obj.ExtensionDictionary != ObjectId.Null)
                {
                    var dict = (DBDictionary)tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
                    if (dict.Contains(entryName))
                    {
                        dict.UpgradeOpen();
                        dict.Remove(entryName);
                        ret = true;
                    }
                }

                tr.Commit();
            }

            return ret;
        }
    }
}
