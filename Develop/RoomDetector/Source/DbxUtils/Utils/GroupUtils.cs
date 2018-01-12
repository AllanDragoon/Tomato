using System;
using System.Collections;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace DbxUtils.Utils
{
    public static class GroupUtils
    {
        /// <summary>
        /// Create Group contains some entities
        /// </summary>
        /// <param name="db"></param>
        /// <param name="grpPattern">A variable for the group's pattern</param>
        /// <param name="ids">Group entities</param>
        public static ObjectId CreateNewGroup(Database db, string grpPattern, ObjectIdCollection ids)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // Get the group dictionary from the drawing
                var gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);

                // Check the group name, to see whether it's already in use
                // 
                int i = 1;
                string grpName;
                do
                {
                    grpName = String.Format("{0}{1}", grpPattern, i);
                    try
                    {
                        // Validate the provided symbol table name
                        //SymbolUtilityServices.ValidateSymbolName(grpName, false);

                        // Only set the group name if it isn't in use
                        if (gd.Contains(grpName))
                        {
                            grpName = String.Empty;
                            i++;
                        }
                    }
                    catch
                    {
                        // An exception has been thrown, indicating the
                        // name is invalid
                        throw new Exception("Invalid group name.");
                    }
                } while (String.IsNullOrEmpty(grpName));

                // Create our new group...
                var grp = new Group("", true);

                // Add the new group to the dictionary
                gd.UpgradeOpen();
                ObjectId grpId = gd.SetAt(grpName, grp);
                tr.AddNewlyCreatedDBObject(grp, true);

                // Add some lines to the group to form a square
                // (the entities belong to the model-space)
                grp.InsertAt(0, ids);

                // Commit the transaction
                tr.Commit();
                return grpId;
            }
        }

        public static void AppendEntitiesIntoGroup(Database db, ObjectId groupId, ObjectIdCollection ids)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var grp = tr.GetObject(groupId, OpenMode.ForRead) as Group;
                if (grp != null)
                {
                    // Add the new group to the dictionary
                    grp.UpgradeOpen();
                    // Add some lines to the group to form a square
                    // (the entities belong to the model-space)
                    grp.Append(ids);
                }
                // Commit the transaction
                tr.Commit();
            }
        }

        public static void AppendEntityIntoGroup(Database db, ObjectId groupId, ObjectId id)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var group = tr.GetObject(groupId, OpenMode.ForRead) as Group;
                if (group != null)
                {
                    // Add the new group to the dictionary
                    group.UpgradeOpen();

                    // Add to goup if it is not add to group yet
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && !group.Has(entity))
                    {
                        group.Append(id);
                    }
                }

                // Commit the transaction
                tr.Commit();
            }
        }

        public static void CleanupGroupEntities(Database db, ObjectId groupId)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var grp = tr.GetObject(groupId, OpenMode.ForRead) as Group;
                if (grp != null)
                {
                    // Add the new group to the dictionary
                    grp.UpgradeOpen();

                    foreach (ObjectId id in grp.GetAllEntityIds())
                    {
                        if (!id.ObjectExists())
                        {
                            grp.Remove(id);
                        }
                    }
                }
                // Commit the transaction
                tr.Commit();
            }
        }

        public static ObjectId[] GetGroupedObjects(Transaction tr, Entity entity)
        {
            var groupId = FindEntityGroupId(tr, entity);

            if (!groupId.IsNull)
            {
                using (var group = tr.GetObject(groupId, OpenMode.ForRead) as Group)
                {
                    if (group != null)
                        return group.GetAllEntityIds();
                }
            }
            return new ObjectId[0];
    }

        /// <summary>
        /// Find Entity Group Id
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public static ObjectId FindEntityGroupId(ObjectId objectId)
        {
            Database db = objectId.Database;
            using (var trans = db.TransactionManager.StartTransaction())
            {
                var obj = trans.GetObject(objectId, OpenMode.ForRead);
                var objId = FindEntityGroupId(trans, obj);
                trans.Commit();
                return objId;
            }
        }

        public static void RemoveGroup(ObjectId groupId)
        {
            using (var tr = groupId.Database.TransactionManager.StartTransaction())
            {
                // Get the group dictionary from the drawing
                RemoveGroup(tr, groupId);

                tr.Commit();
            }
        }

        public static void RemoveGroup(Transaction tr, ObjectId groupId)
        {
            // Get the group dictionary from the drawing
            var gd = (DBDictionary)tr.GetObject(groupId.Database.GroupDictionaryId, OpenMode.ForRead);

            var group = tr.GetObject(groupId, OpenMode.ForRead) as Group;
            if (group != null)
            {
                // Does the anonymous group have
                // any members associated with it?
                var numItems = group.NumEntities;
                if (numItems > 0)
                {
                    // Empty the group
                    // Upgrade it first
                    group.UpgradeOpen();
                    group.Clear();
                    group.DowngradeOpen();
                }

                // Add the new group to the dictionary
                gd.UpgradeOpen();
                // Get the group ID and remove the group from the dictionary
                gd.Remove(groupId);
            }
        }

        /// <summary>
        /// Find Entity Group Id
        /// </summary>
        /// <param name="trans"></param>
        /// <param name="dbObject"></param>
        /// <returns></returns>
        public static ObjectId FindEntityGroupId(Transaction trans, DBObject dbObject)
        {
            var col = dbObject.GetPersistentReactorIds();
            if (col != null)
            {
                foreach (ObjectId id in col)
                {
                    using (DBObject obj = trans.GetObject(id, OpenMode.ForRead))
                    {
                        if (obj is Group)
                        {
                            return obj.ObjectId;
                        }
                    }
                }
            }

            return ObjectId.Null;
        }

        public static ObjectId GetNewestGroupId(Database database)
        {
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var groupDictionary = transaction.GetObject(database.GroupDictionaryId, OpenMode.ForRead) as DBDictionary;
                return GetNewestIdFromDictionary(groupDictionary);
            }
        }

        public static ObjectId GetNewestIdFromDictionary(DBDictionary dictionary)
        {
            ICollection ids = ((IDictionary)dictionary).Values;
            ObjectId last = ObjectId.Null;
            foreach (ObjectId temp in ids)
            {
                last = temp;
                break;
            }
            if (!last.IsNull)
            {
                long current = last.Handle.Value;
                foreach (ObjectId id in ids)
                {
                    long value = id.Handle.Value;
                    if (value > current)
                    {
                        current = value;
                        last = id;
                    }
                }
            }
            return last;
        }
    }
}
