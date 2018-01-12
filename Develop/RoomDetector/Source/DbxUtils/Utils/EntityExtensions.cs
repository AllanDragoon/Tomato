using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DbxUtils.Utils
{
	/// <summary>
	/// Provides a set of extension methods to entity.
	/// </summary>
	public static class EntityExtensions
	{
        public static ObjectId AddToCurrentSpace(this Entity ent)
        {
            return ent.AddToCurrentSpace(HostApplicationServices.WorkingDatabase);
        }

		/// <summary>
		/// Add entity to the specified space of the given database
		/// </summary>
		public static ObjectId AddToSpace(this Entity entity, Database database, ObjectId spaceId)
		{
			if (entity == null) throw new ArgumentNullException(/*MSG0*/"entity"); 
			if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				var dbSpace = (BlockTableRecord)transaction.GetObject(spaceId, OpenMode.ForWrite);
				ObjectId id = dbSpace.AppendEntity(entity);

				transaction.AddNewlyCreatedDBObject(entity, true);
				transaction.Commit();

				return id;
			}
		}

		/// <summary>
		/// Add entity to the model space of the working database
		/// </summary>
		/// <param name="entity">Arx Entity</param>
		/// <param name="database">Arx database</param>
		/// <returns>ObjectId of the appended entity</returns>
		public static ObjectId AddToModelSpace(this Entity entity, Database database)
		{
			return AddToSpace(entity, database, SymbolUtilityServices.GetBlockModelSpaceId(database));
		}

		/// <summary>
		/// Add entity to the paper space of the working database
		/// </summary>
		/// <param name="entity">Arx Entity</param>
		/// <param name="database">Arx database</param>
		/// <returns>ObjectId of the appended entity</returns>
		public static ObjectId AddToPaperSpace(this Entity entity, Database database)
		{
			return AddToSpace(entity, database, SymbolUtilityServices.GetBlockPaperSpaceId(database));
		}

		/// <summary>
		/// Add entity to the current space of the working database
		/// </summary>
		/// <param name="entity">Arx Entity</param>
		/// <param name="database">Arx database</param>
		/// <returns>ObjectId of the appended entity</returns>
		public static ObjectId AddToCurrentSpace(this Entity entity, Database database)
		{
			if (database == null) throw new ArgumentNullException(/*MSG0*/"database");
			return AddToSpace(entity, database, database.CurrentSpaceId);
		}

		static void SetXData(this DBObject dbObject, Action<ResultBuffer> addAction, Transaction transactionToBeCommitted)
		{
			if (dbObject == null) throw new ArgumentNullException(/*MSG0*/"dbObject");
			if (transactionToBeCommitted == null) throw new ArgumentNullException(/*MSG0*/"transactionToBeCommitted");

			using (var rb = new ResultBuffer())
			{
				addAction(rb);
				dbObject.XData = rb;
				transactionToBeCommitted.Commit();
			}
		}

		/// <summary>
		/// Set "XData" on an Entity w/o directly manipulating a ResultBuffer.
		/// </summary>
		/// <param name="transactionToBeCommitted">An open transaction that will be Commit()ted</param>
		public static void SetXData(this DBObject dbObject, string data, DxfCode code, Transaction transactionToBeCommitted)
		{
			if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(/*MSG0*/"data");

			dbObject.SetXData(rb => rb.AddValue(data, code), transactionToBeCommitted);
		}

		/// <summary>
		/// Set "XData" on an Entity w/o directly manipulating a ResultBuffer.
		/// </summary>
		/// <param name="transactionToBeCommitted">An open transaction that will be Commit()ted</param>
		public static void SetXData(this DBObject dbObject, IEnumerable<TypedValue> collection, Transaction transactionToBeCommitted)
		{
			if (collection == null) throw new ArgumentNullException(/*MSG0*/"collection");

			dbObject.SetXData(rb => rb.AddRange(collection), transactionToBeCommitted);
		}

        public static void Rotate(this Entity ent, Point3d basePoint, double rotationAngle)
        {
            ent.ObjectId.Rotate(basePoint, rotationAngle);
        }

        public static void Move(this Entity ent, Point3d fromPoint, Point3d toPoint)
        {
            ent.ObjectId.Move(fromPoint, toPoint);
        }

        public static void Scale(this Entity ent, Point3d basePoint, double scaleFactor)
        {
            ent.ObjectId.Scale(basePoint, scaleFactor);
        }

        public static ObjectId Mirror(this Entity ent, Point3d mirrorPoint1, Point3d mirrorPoint2, bool eraseSourceObject)
        {
            return ent.ObjectId.Mirror(mirrorPoint1, mirrorPoint2, eraseSourceObject);
        }

        public static ObjectId Copy(this Entity ent)
        {
            return ent.ObjectId.Copy();
        }

        public static void ArrayPolar(this Entity ent, Point3d cenPt, int numObj, double angle)
        {
            var workingDatabase = ent.Database;
            using (var transaction = workingDatabase.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(workingDatabase.BlockTableId, 0, false);
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                for (var i = 0; i < numObj - 1; i++)
                {
                    var matrix3D = Matrix3d.Rotation(angle * (double)(i + 1) / (double)numObj, Vector3d.ZAxis, cenPt);
                    var transformedCopy = ent.GetTransformedCopy(matrix3D);
                    blockTableRecord.AppendEntity(transformedCopy);
                    transaction.AddNewlyCreatedDBObject(transformedCopy, true);
                }
                transaction.Commit();
            }
        }

        public static void ArrayRectangular(this Entity ent, int numRows, int numCols, double disRows, double disCols)
        {
            var workingDatabase = ent.Database;
            using (var transaction = workingDatabase.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(workingDatabase.BlockTableId, 0, false);
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                for (var i = 0; i < numRows; i++)
                {
                    for (var j = 0; j < numCols; j++)
                    {
                        var matrix3D = Matrix3d.Displacement(new Vector3d((double)j * disCols, (double)i * disRows, 0.0));
                        var transformedCopy = ent.GetTransformedCopy(matrix3D);
                        blockTableRecord.AppendEntity(transformedCopy);
                        transaction.AddNewlyCreatedDBObject(transformedCopy, true);
                    }
                }
                ent.Erase();
                transaction.Commit();
            }
        }
	}
}
