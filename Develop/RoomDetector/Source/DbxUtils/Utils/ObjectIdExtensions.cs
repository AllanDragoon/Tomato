using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;
using System.Windows;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DbxUtils.Utils
{
	/// <summary>
	/// General purpose extension methods for AutoCAD Transactions
	/// </summary>
	public static class TransactionExtensions
	{
		/// <summary>
		/// Call GetObject() on a strongly-typed ObjectId using the specified transaction
		/// </summary>
		public static TDbObject GetObjectT<TDbObject>(this Transaction transaction, ObjectId<TDbObject> id, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			return (TDbObject)transaction.GetObject(id, mode, openErased, forceOpenOnLockedLayer);
		}

		/// <summary>
		/// Calls Transaction.GetObject(id) for each item in the enumeration
		/// </summary>
		public static IEnumerable<TDbObject> GetObjects<TDbObject>(this Transaction transaction,
			IEnumerable<ObjectId<TDbObject>> objectIds, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			Contract.Requires(transaction != null);
			Contract.Requires(objectIds != null);

			var objects = from objectId in objectIds
						  select transaction.GetObjectT(objectId, mode, openErased, forceOpenOnLockedLayer);
			return objects;
		}
	}

	/// <summary>
	/// General purpose extension methods for AutoCAD ObjectIds
	/// </summary>
	public static class ObjectIdExtensionsT // ObjectId<TDBObject>
	{
		/// <summary>
		/// Calls id.GetObject() for each item in the enumeration
		/// </summary>
		public static IEnumerable<TDbObject> GetObjects<TDbObject>(this IEnumerable<ObjectId<TDbObject>> objectIds,
			OpenMode mode, bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			var objects = from objectId in objectIds
						  select objectId.GetObject(mode, openErased, forceOpenOnLockedLayer);
			return objects;
		}

		/// <summary>
		/// Open the given objectId inside of a new transaction and then call the specified function;
		/// the transaction will be Commit()ted.
		/// </summary>
		public static TResult UsingWithTransaction<TDBObject, TResult>(this ObjectId<TDBObject> id, Func<TDBObject, TResult> f, OpenMode mode,
			bool commit = false,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDBObject : DBObject
		{
			return id.Id.UsingWithTransaction(f, mode, commit, openErased, forceOpenOnLockedLayer);
		}

		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForRead
		/// </summary>
		public static TResult ForRead<TDbObject, TResult>(this ObjectId<TDbObject> id, Func<TDbObject, TResult> f,
			bool commit = false,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			return id.Id.ForRead(f, commit, openErased, forceOpenOnLockedLayer);
		}

		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForWrite
		/// </summary>
		public static TResult ForWrite<TDbObject, TResult>(this ObjectId<TDbObject> id, Func<TDbObject, TResult> f,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			return id.Id.ForWrite(f, openErased, forceOpenOnLockedLayer);
		}
		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForWrite, converting the Action to a Func
		/// </summary>
		public static void ForWrite<TDbObject>(this ObjectId<TDbObject> id, Action<TDbObject> a,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			id.Id.ForWrite(a, openErased, forceOpenOnLockedLayer);
		}
	}

	/// <summary>
	/// General purpose extension methods for AutoCAD ObjectIds
	/// </summary>
	public static class ObjectIdExtensions
	{
		/// <summary>
		/// Open the given objectId inside of a new transaction and then call the specified function.
		/// </summary>
		/// <param name="commit">Should the created transaction be Commit()ted? The default is "false"</param>
		public static TResult UsingWithTransaction<TDbObject, TResult>(this ObjectId id, Func<TDbObject, TResult> f, OpenMode mode,
			bool commit = false,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			// DO NOT make this routine any more complicated! (see Use())
			// This is intended to be a simple wrapper around the idiom below and NOTHING else!
			// If you need something more complicated, look at Use() or write something else.
			using (var t = id.Database.TransactionManager.StartTransaction())
			{
				var dbObj = (TDbObject) t.GetObject(id, mode, openErased, forceOpenOnLockedLayer);
				var retval = f(dbObj); // back to calling code

				// The "commit" flag above defaults to "false" because Transactions are required to be
				// explicitly Commit()ted; the default (if nothing is done) is to Abort() when Dispose()d.
				//
				// Also, while Commit() can be faster than Abort() (AutoCAD doesn't have to roll back the changes,
				// just turn things over to the containing transaction), it hide incorrect code: an object is opened
				// in a containing transaction ForWrite, it is then "opened" here ForRead but the lambda inadvertently
				// makes a change to the object.  AutoCAD won't complain because the object is really already
				// open ForWrite.  If the transaction is Commit()ted, the changes will be applied and nothing will
				// go wrong until the same code is executed w/o the containing transaction first opening ForWrite.
				// At least with an Abort(), the incorrect (changing with ForRead) lambda won't really do anything.
				if (commit)
					t.Commit();

				return retval;
			}
		}

		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForRead
		/// </summary>
		public static TResult ForRead<TDbObject, TResult>(this ObjectId id, Func<TDbObject, TResult> f,
			bool commit = false,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			lock (Runtime.DBSyncObject)
            {
                return id.UsingWithTransaction(f, OpenMode.ForRead, commit, openErased, forceOpenOnLockedLayer);
            }
		}

		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForWrite
		/// </summary>
		public static TResult ForWrite<TDbObject, TResult>(this ObjectId id, Func<TDbObject, TResult> f,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			lock (Runtime.DBSyncObject)
            {
                // Why open ForWrite only to rollback any changes?
                return id.UsingWithTransaction(f, OpenMode.ForWrite, true /*commit*/, openErased, forceOpenOnLockedLayer);
            }
		}
		/// <summary>
		/// Call UsingWithTransaction() with OpenMode.ForWrite, converting the Action to a Func
		/// </summary>
		public static void ForWrite<TDbObject>(this ObjectId id, Action<TDbObject> a,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			lock (Runtime.DBSyncObject)
            {
                id.ForWrite(
                    (TDbObject dbObj) => { a(dbObj); return IntPtr.Zero; }, // turn Action<> into Func<IntPtr>
                    openErased, forceOpenOnLockedLayer);
            }
		}

		// ForNotify() is INTENTIONALLY omitted; from the ObjectARX docs: "... this mode should be used sparingly, ..."
		// So, don't make it easy to use something that shouldn't be used very often.  In any case, calling
		// UsingWithTransaction() directly isn't that much more difficult.

		/// <summary>
		/// Convert a collection of ObjectIds into strongly-typed ObjectIds
		/// </summary>
		public static IEnumerable<ObjectId<TDbObject>> AsEnumerable<TDbObject>(this IEnumerable<ObjectId> ids) where TDbObject : DBObject
		{
			Contract.Requires(ids != null);

			var retval = from id in ids
						 where !id.IsNull // ObjectId<TDBObject>() fails on Null ObjectIds
						 select new ObjectId<TDbObject>(id);
			return retval;
		}

		static IEnumerable<TDbObject> OfType<TDbObject>(IEnumerable<ObjectId> objectIds, Func<ObjectId, TDbObject> getObject) where TDbObject : DBObject
		{
			Contract.Requires(objectIds != null);

			var retval = from id in objectIds
						 let dbObjectT = (TDbObject) getObject(id) // cast is OK, getObject() should return right type or "null"
						 where dbObjectT != null
						 select dbObjectT;
			return retval;
		}

		/// <summary>
		/// Open each of the ObjectIds in an existing transaction returning the opened object if it is of the specified type
		/// </summary>
		public static IEnumerable<TDbObject> OfType<TDbObject>(this IEnumerable<ObjectId> objectIds, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			Contract.Requires(objectIds != null);
			return OfType(objectIds, id => id.GetObject(mode, openErased, forceOpenOnLockedLayer) as TDbObject); // uses TopTransaction
		}

		/// <summary>
		/// Open each of the ObjectIds in the specified transaction returning the opened object if it is of the specified type
		/// </summary>
		public static IEnumerable<TDbObject> OfType<TDbObject>(IEnumerable<ObjectId> objectIds, Transaction transaction, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			Contract.Requires(objectIds != null);
			Contract.Requires(transaction != null);
			return OfType(objectIds, id => transaction.GetObject(id, mode, openErased, forceOpenOnLockedLayer) as TDbObject);
		}

		/// <summary>
		/// Open each of the ObjectIds in an existing transaction converting (casting) to the specified type
		/// </summary>
		public static IEnumerable<TDbObject> Cast<TDbObject>(this IEnumerable<ObjectId> objectIds, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			Contract.Requires(objectIds != null);
			IEnumerable<ObjectId<TDbObject>> ids = objectIds.AsEnumerable<TDbObject>();
			return ids.GetObjects(mode, openErased, forceOpenOnLockedLayer); // uses TopTransaction
		}

		/// <summary>
		/// Open each of the ObjectIds in the specified existing transaction converting (casting) to the specified type
		/// </summary>
		public static IEnumerable<TDbObject> Cast<TDbObject>(this IEnumerable<ObjectId> objectIds, Transaction transaction, OpenMode mode,
			bool openErased = false, bool forceOpenOnLockedLayer = false) where TDbObject : DBObject
		{
			Contract.Requires(objectIds != null);
			Contract.Requires(transaction != null);

			IEnumerable<ObjectId<TDbObject>> ids = objectIds.AsEnumerable<TDbObject>();
			return transaction.GetObjects(ids, mode, openErased, forceOpenOnLockedLayer);
		}

        public static void SetXData(this ObjectId entId, string applicationName, ResultBuffer xData)
        {
            var transactionManager = entId.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var regAppTable = transaction.GetObject(entId.Database.RegAppTableId, OpenMode.ForWrite) as RegAppTable;
                if (!regAppTable.Has(applicationName))
                {
                    var regAppTableRecord = new RegAppTableRecord { Name = applicationName };
                    regAppTable.Add(regAppTableRecord);
                    transaction.AddNewlyCreatedDBObject(regAppTableRecord, true);
                }
                var entity = transaction.GetObject(entId, OpenMode.ForWrite) as Entity;
                if (entity != null)
                {
                    entity.XData = xData;
                }
                transaction.Commit();
            }
        }

        public static ResultBuffer GetXData(this ObjectId entId, string applicationName)
        {
            var result = new ResultBuffer();
            var transactionManager = entId.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = transaction.GetObject(entId, 0) as Entity;
                if (entity != null)
                {
                    result = entity.GetXDataForApplication(applicationName);
                }
                transaction.Commit();
            }
            return result;
        }

        public static void SetXRecord(this ObjectId entId, string keyForXrecord, ResultBuffer xRecord)
        {
            var transactionManager = entId.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var xrecord = new Xrecord { Data = xRecord };
                var entity = transaction.GetObject(entId, OpenMode.ForWrite) as Entity;
                if (entity != null)
                {
                    if (entity.ExtensionDictionary != ObjectId.Null)
                    {
                        // Editor.WriteMessage("Cannot create the Xrecord,because the entity already has XRecord");
                        return;
                    }
                    entity.CreateExtensionDictionary();

                    var extensionDictionary = entity.ExtensionDictionary;
                    var dBDictionary = transaction.GetObject(extensionDictionary, OpenMode.ForWrite) as DBDictionary;
                    dBDictionary.SetAt(keyForXrecord, xrecord);
                    transaction.AddNewlyCreatedDBObject(xrecord, true);
                }
                transaction.Commit();
            }
        }

        public static ResultBuffer GetXRecord(this ObjectId entId, string keyForXrecord)
        {
            var result = new ResultBuffer();
            var transactionManager = entId.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = transaction.GetObject(entId, 0) as Entity;
                if (entity != null)
                {
                    var dBDictionary = transaction.GetObject(entity.ExtensionDictionary, 0) as DBDictionary;
                    if (dBDictionary != null)
                    {
                        var at = dBDictionary.GetAt(keyForXrecord);
                        if (at != ObjectId.Null)
                        {
                            var xrecord = transaction.GetObject(at, 0) as Xrecord;
                            if (xrecord != null)
                            {
                                result = xrecord.Data;
                            }
                        }
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        public static void Rotate(this ObjectId id, Point3d basePoint, double rotationAngle)
        {
            var matrix3D = Matrix3d.Rotation(rotationAngle * 3.1415926535897931 / 180.0, new Vector3d(0.0, 0.0, 1.0), basePoint);
            using (var transaction = id.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)id.Database.TransactionManager.GetObject(id, OpenMode.ForWrite, true);
                entity.TransformBy(matrix3D);
                transaction.Commit();
            }
        }

        public static void Move(this ObjectId id, Point3d fromPoint, Point3d toPoint)
        {
            var vector3D = toPoint - fromPoint;
            var matrix3D = Matrix3d.Displacement(vector3D);
            var transactionManager = id.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(id, OpenMode.ForWrite, true);
                entity.TransformBy(matrix3D);
                transaction.Commit();
            }
        }

        public static void Move(this ObjectId id, Vector3d fromPoint, Vector3d toPoint)
        {
            var vector3D = toPoint - fromPoint;
            var matrix3D = Matrix3d.Displacement(vector3D);
            var transactionManager = id.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(id, OpenMode.ForWrite, true);
                entity.TransformBy(matrix3D);
                transaction.Commit();
            }
        }

        public static void Scale(this ObjectId id, Point3d basePoint, double scaleFactor)
        {
            var matrix3D = Matrix3d.Scaling(scaleFactor, basePoint);
            var transactionManager = id.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(id, OpenMode.ForWrite, true);
                entity.TransformBy(matrix3D);
                transaction.Commit();
            }
        }

        public static ObjectId Mirror(this ObjectId idSource, Point3d mirrorPoint1, Point3d mirrorPoint2, bool eraseSourceObject)
        {
            var line3D = new Line3d(mirrorPoint1, mirrorPoint2);
            var matrix3D = Matrix3d.Mirroring(line3D);
            var objectId = idSource.Copy();
            var transactionManager = idSource.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(objectId, OpenMode.ForWrite, true);
                entity.TransformBy(matrix3D);
                transaction.Commit();
            }
            if (eraseSourceObject)
            {
                idSource.Erase();
            }
            return objectId;
        }

        public static ObjectId Copy(this ObjectId idCopy)
        {
            var transactionManager = idCopy.Database.TransactionManager;
            ObjectId result;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(idCopy, 0, true);
                var ent = (Entity)entity.Clone();
                result = ent.AddToCurrentSpace();
                transaction.Commit();
            }
            return result;
        }

        static Entity GetEntity(ObjectId id)
        {
            Entity result;
            using (var transaction = id.Database.TransactionManager.StartTransaction())
            {
                result = (Entity)id.Database.TransactionManager.GetObject(id, 0, true);
                transaction.Commit();
            }
            return result;
        }

        public static void Erase(this ObjectId id)
        {
            var transactionManager = id.Database.TransactionManager;
            using (var transaction = transactionManager.StartTransaction())
            {
                var entity = (Entity)transactionManager.GetObject(id, OpenMode.ForWrite, true);
                entity.Erase();
                transaction.Commit();
            }
        }

        public static void ArrayPolar(this ObjectId id, Point3d cenPt, int numObj, double angle)
        {
            var workingDatabase = id.Database;
            using (var transaction = workingDatabase.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(workingDatabase.BlockTableId, 0, false);
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                var entity = (Entity)transaction.GetObject(id, OpenMode.ForWrite);
                for (var i = 0; i < numObj - 1; i++)
                {
                    var matrix3D = Matrix3d.Rotation(angle * (double)(i + 1) / (double)numObj, Vector3d.ZAxis, cenPt);
                    var transformedCopy = entity.GetTransformedCopy(matrix3D);
                    blockTableRecord.AppendEntity(transformedCopy);
                    transaction.AddNewlyCreatedDBObject(transformedCopy, true);
                }
                transaction.Commit();
            }
        }

        public static void ArrayRectangular(this ObjectId id, int numRows, int numCols, double disRows, double disCols)
        {
            var workingDatabase = id.Database;
            using (var transaction = workingDatabase.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(workingDatabase.BlockTableId, 0, false);
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                var entity = (Entity)transaction.GetObject(id, OpenMode.ForWrite);
                for (var i = 0; i < numRows; i++)
                {
                    for (var j = 0; j < numCols; j++)
                    {
                        var matrix3D = Matrix3d.Displacement(new Vector3d((double)j * disCols, (double)i * disRows, 0.0));
                        var transformedCopy = entity.GetTransformedCopy(matrix3D);
                        blockTableRecord.AppendEntity(transformedCopy);
                        transaction.AddNewlyCreatedDBObject(transformedCopy, true);
                    }
                }
                entity.Erase();
                transaction.Commit();
            }
        }

	    public static bool IsValidObjectId(this ObjectId id)
	    {
	        return !id.IsNull && id.IsValid;
	    }

        public static bool ObjectExists(this ObjectId id)
        {
            return !id.IsNull && id.IsValid && !id.IsErased;
        }

        public static bool CheckObjectIdZoomable(this ObjectId objectId)
	    {
            // 细分 invalid的情况，如果objectid是空或者无效，那认为是此objectid不在database中；
            if (objectId.IsNull || !objectId.IsValid)
            {
                MessageBox.Show("未能成功定位到图形", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            // 如果不为空且有效，同时又是Erased的状态，那说明用户把此地块所对应的图形删除了。
            if (objectId.IsErased)
            {
                MessageBox.Show("该图形已被删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            // 判断objectiId所在的blockId是否是当前active的空间一致。如不一致说明当前model/paper space中没有objectId。
            bool isObjectInCurrentSpace = true;
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)transaction.GetObject(objectId, OpenMode.ForRead);
                isObjectInCurrentSpace = (objectId.Database.CurrentSpaceId == entity.BlockId);
                transaction.Commit();
            }

            if (!isObjectInCurrentSpace)
            {
                MessageBox.Show("当前空间没有指定的实体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
	    }
	}
}
