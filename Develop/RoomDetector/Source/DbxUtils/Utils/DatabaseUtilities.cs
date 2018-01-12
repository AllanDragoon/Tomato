using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using UnitType = DbxUtils.Units.UnitType;

namespace DbxUtils.Utils
{
    /// <summary>
    /// Static RealDWG.NET-based utility functions.
    /// </summary>
    public static class DatabaseUtilities
    {
        /// <summary>
        /// Load the specified DWG file.
        /// </summary>
        /// <param name="filePath">Full name of the DWG file to load.</param>
        /// <param name="readOnly">Open file read-only if true, otherwise open for write.</param>
        /// <param name="loadXrefs">Load all xref files if true.</param>
        /// <returns>Database reference of the loaded DWG file.</returns>
        public static Database LoadDwg(string filePath, bool readOnly, bool loadXrefs)
        {
            // Create a database object and set it as the working database.
            //
            var database = new Database(buildDefaultDrawing: false, noDocument: true);
            //HostApplicationServices.WorkingDatabase = database;

            // Load the Dwg file.
            //
            if (readOnly)
                database.ReadDwgFile(filePath, FileOpenMode.OpenTryForReadShare, true, null);
            else
                database.ReadDwgFile(filePath, FileShare.ReadWrite, false, null);

            // If we successfully loaded the DWG and were asked to resolve the xrefs,
            // then do that now.
            //
            if (database != null && loadXrefs)
                database.ResolveXrefs(false, true);

            return database;
        }

		/// <summary>
		/// Create a default DWG file
		/// </summary>
		/// <returns></returns>
		public static Database CreateDwg()
		{
			var database = new Database(true, true);
			//HostApplicationServices.WorkingDatabase = database;

			return database;
		}

		/// <summary>
		/// Create a default DWG file.
		/// This will be called from C++, which doesn't support default parameter.
		/// </summary>
		/// <returns></returns>
        public static Database CreateDwg(string filename, string templateName, bool setAsWorkingDatabase)
		{
			Database database = null;

			// Read the template.
			if (!String.IsNullOrEmpty(templateName) && File.Exists(templateName))
				database = LoadDwg(templateName, readOnly: true, loadXrefs: false);
			else
				database = new Database(buildDefaultDrawing:true, noDocument:true);

            if (setAsWorkingDatabase)
			    HostApplicationServices.WorkingDatabase = database;

			// Save out as the new name.
			if (!String.IsNullOrEmpty(filename))
				database.SaveAs(filename, DwgVersion.Current);

		    return database;
		}

        /// <summary>
        /// Save and close database.
        /// </summary>
        /// <param name="database">The dwg database</param>
        public static void SaveAndCloseDwg(Database database)
        {
            database.Save();
			CloseDwg(database);
        }

        /// <summary>
        /// Save and close database.
        /// </summary>
        /// <param name="database">The dwg database</param>
        public static void SaveAsAndCloseDwg(Database database, string filename, DwgVersion dwgVersion)
        {
            database.SaveAs(filename, dwgVersion);
            CloseDwg(database);
        }

        /// <summary>
        /// Dispose database without save.
        /// </summary>
        /// <param name="database">The dwg database</param>
        public static void CloseDwg(Database database)
        {
            //HostApplicationServices.WorkingDatabase = null;
            database.Dispose();
        }

        /// <summary>
        /// Get Layer names from DWG
        /// </summary>
        /// <param name="filePath">Full name of the DWG file to load.</param>
        public static IEnumerable<string> GetDwgLayers(string filePath)
        {
            var database = DatabaseUtilities.LoadDwg(filePath, readOnly: true, loadXrefs: true);

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                // Start by populating the list of names/IDs of Layer
                var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                foreach (var entId in layerTable)
                {
                    var dbObject = transaction.GetObject(entId, OpenMode.ForRead);
                    var layerTableRecord = dbObject as LayerTableRecord;
                    if (layerTableRecord != null && layerTableRecord.IsErased == false)
                    {
                        yield return layerTableRecord.Name;
                    }
                }
                transaction.Commit();
            }

            DatabaseUtilities.CloseDwg(database);
        }

        /// <summary>
        /// Determine if the unit of a DWG file is metric
        /// </summary>
        /// <param name="filePath">Full name of the DWG file to load.</param>
        public static bool IsMetricDwg(string filePath)
        {
            Database database = DatabaseUtilities.LoadDwg(filePath, readOnly: true, loadXrefs: false);
            UnitType unitType = DbUnitUtils.GetUnitType(database);
            DatabaseUtilities.CloseDwg(database);

            return unitType == UnitType.Metric;
        }

		/// <summary>
		/// Get the units conversion factor from the original AutoCAD database.  The
		/// conversion factor is required since Inventor works internally in centimeters.
		/// </summary>
		/// <param name="database">the database which we want to get unit from</param>
		/// <returns></returns>
		static public double GetUnitsConversionFactor(Database database)
		{
			if (database == null) throw new ArgumentNullException("database");

            var conversionFactor = 1.0;
            var insUnits = database.Insunits;

			if (insUnits == UnitsValue.Meters)
				conversionFactor = 100.0;
			else if (insUnits == UnitsValue.Inches)
				conversionFactor = 2.54;
			else if (insUnits == UnitsValue.Centimeters)
				conversionFactor = 1.0;
			else if (insUnits == UnitsValue.Millimeters)
				conversionFactor = 0.1;
			else if (insUnits == UnitsValue.Microns)
				conversionFactor = 0.0001;
			else if (insUnits == UnitsValue.Feet)
				conversionFactor = 30.48; // 12.0 * inch (2.54)
			else
			{
			    var eMeasurement = database.Measurement;
			    conversionFactor = eMeasurement == MeasurementValue.English ? 2.54 : 0.1;
			}

		    return conversionFactor;
		}

		// This represents conversion factory from feet to centimeters (12.0 * inch (2.54))
		public const double FactoryUnitToInventorDbUnitRatio = 30.48;

		/// <summary>
		/// Get unit conversion factor from  feet to ACAD database's insunits
		/// </summary>
		/// <param name="database"></param>
		/// <returns></returns>
		public static double GetFeetToInsunitsConversionFactor(Database database)
		{
			if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

			switch (database.Insunits)
			{
				case UnitsValue.Undefined:
			        {
			            switch (database.Measurement)
			            {
			                case MeasurementValue.English:
			                    return 12.0;
			                case MeasurementValue.Metric:
			                    return 1 / 0.003281;
			            }

			            throw new InvalidOperationException();
			        }

			    case UnitsValue.Inches: return 12.0;
				case UnitsValue.Feet: return 1.0;
				case UnitsValue.Miles: return 1 / 5280.0;
				case UnitsValue.Millimeters: return 304.8;
				case UnitsValue.Centimeters: return 30.48;
				case UnitsValue.Meters: return 0.3048;
				case UnitsValue.Kilometers: return 0.0003048;
				case UnitsValue.MicroInches: return 0.12e8;
				case UnitsValue.Mils: return 12000;
				case UnitsValue.Yards: return 1 / 3.0;
				case UnitsValue.Angstroms: return 1 / 3.281e-10;
				case UnitsValue.Nanometers: return 1 / 3.281e-9;
				case UnitsValue.Microns: return 1 / 3.281e-6;
				case UnitsValue.Decimeters: return 3.048;
				case UnitsValue.Dekameters: return 0.03048;
				case UnitsValue.Hectometers: return 0.003048;
				case UnitsValue.Gigameters: return 3.048e-10;
				case UnitsValue.Astronomical: return 1 / 4.908e11;
				case UnitsValue.LightYears: return 1 / 3.1018e16;
				case UnitsValue.Parsecs: return 1 / 1.0124e17;

				default:
					throw new InvalidOperationException();
			}
		}

		/// <summary>
		/// Use the Factory Unit to Database Unit conversion factor to convert an AutoCAD (3d) Point to database units
		/// </summary>
        /// <param name="pointInFactoryUnit"></param>
		/// <returns></returns>
		public static Point3d AsInventorDbValue(Point3d pointInFactoryUnit)
		{
			return new Point3d(pointInFactoryUnit.X * FactoryUnitToInventorDbUnitRatio,
			                   pointInFactoryUnit.Y * FactoryUnitToInventorDbUnitRatio,
			                   pointInFactoryUnit.Z * FactoryUnitToInventorDbUnitRatio);
		}

		/// <summary>
		/// This function get a sub "DBDictionary" objectId under database's NOD.
		/// </summary>
		/// <param name="database">Database instance in which to get the DBDictionary objectId.</param>
		/// <param name="dictionaryName">Sub dictionary name.</param>
		/// <param name="createForNone">If true, a new DBDictionary will be created and added under NOD if not found.</param>
		/// <returns>Object Id of the retrived Dictionary</returns>
		public static ObjectId GetSubDictionaryFromNod(Database database, string dictionaryName, bool createForNone)
		{
			if (database == null) throw new ArgumentNullException(/*MSG0*/"database");
			if (String.IsNullOrEmpty(dictionaryName)) throw new ArgumentNullException(/*MSG0*/"dictionaryName");

			var objectId = GetSubDictionaryInDictionary(database.NamedObjectsDictionaryId, dictionaryName, createForNone);
			return objectId;
		}

		/// <summary>
		/// This function get a sub "DBDictionary" objectId in a parent DBDictionary.
		/// </summary>
		/// <param name="parentDictionaryId">Parent Dictionary Id</param>
		/// <param name="childDictionaryName">Sub dictionary name</param>
		/// <param name="createForNone">If true, a new DBDictionary will be created and added under parent dictionary if not found.</param>
		/// <returns>Object Id of the retrieved Dictionary</returns>
		public static ObjectId GetSubDictionaryInDictionary(ObjectId parentDictionaryId, string childDictionaryName, bool createForNone = true)
		{
			if (parentDictionaryId.IsNull) throw new ArgumentNullException(/*MSG0*/"parentDictionaryId");
			if (String.IsNullOrEmpty(childDictionaryName)) throw new ArgumentNullException(/*MSG0*/"childDictionaryName");

            var dictId = ObjectId.Null;

			using (Transaction myTrans = parentDictionaryId.Database.TransactionManager.StartTransaction())
			{
				var parentDictionary = (DBDictionary)myTrans.GetObject(parentDictionaryId, OpenMode.ForRead);

				if (parentDictionary.Contains(childDictionaryName))
				{
					dictId = parentDictionary.GetAt(childDictionaryName);
				}
				else if (createForNone)
				{
					// Create a new dictionary and add it.
                    using (var factoryDictory = new DBDictionary())
					{
						parentDictionary.UpgradeOpen();
						parentDictionary.SetAt(childDictionaryName, factoryDictory);
						myTrans.AddNewlyCreatedDBObject(factoryDictory, true);
						dictId = factoryDictory.ObjectId;
					}
				}

				myTrans.Commit();
			}

			return dictId;
		}

        /// <summary>
        /// Add the specified entity to the model space block of the specified database.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <param name="database">The database whose model space block the entity is added to.</param>
        /// <returns>The object id of the entity added to the database.</returns>
		public static ObjectId AddEntityToModelSpace(Entity entity, Database database)
		{
            ObjectId objId;

            using (var transaction = database.TransactionManager.StartTransaction())
			{
                var modelspace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
				objId = modelspace.AppendEntity(entity);
				transaction.AddNewlyCreatedDBObject(entity, true);
				transaction.Commit();
			}

			return objId;
		}

		/// <summary>
		/// Convert handle value (long) to Object Id
		/// </summary>
		/// <param name="db"></param>
		/// <param name="handleValue"></param>
		/// <returns></returns>
		public static ObjectId Convert(Database db, long handleValue)
		{
			var handle = new Handle(handleValue);
			return db.GetObjectId(false, handle, 0);
		}

		/// <summary>
		/// Convert handle value collection to ObjectId collection
		/// </summary>
		/// <param name="db"></param>
		/// <param name="handleValueCollection"></param>
		/// <returns></returns>
		public static ICollection<ObjectId> Convert(Database db, ICollection<long> handleValueCollection)
		{
            var idList = new List<ObjectId>();

			foreach (long handleValue in handleValueCollection)
			{
                var objId = Convert(db, handleValue);
				if (objId.IsValid)
					idList.Add(objId);
			}

			return idList;
		}

		private const string AEC_VARS_DICTIONARY_NAME = /*MSG0*/"AEC_VARS";
		private const string AEC_VARS_RECORD_NAME = /*MSG0*/"AEC_VARS_DWG_SETUP";

		// in a separate method to make it easier to switch back to .NET 3.5---removing "dynamic"
		static string GetLinearUnitString(DBObject aecvarsDwgSetup)
		{
			// We don't want to add a reference to the AEC assembly, so use reflection via "dynamic"
			// to get the "LinearUnit" property
			Type t = aecvarsDwgSetup.GetType();
			object oUnit = t.InvokeMember(/*MSG0*/"LinearUnit", BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public, null /*binder*/, aecvarsDwgSetup, null /*args*/, // TODO: use "dynamic" in .NET 4.0
				System.Globalization.CultureInfo.InvariantCulture);
			var unit = (Enum)oUnit;
			//Enum unit = ((dynamic)aecvarsDwgSetup).LinearUnit;

			if (unit.GetType().FullName != /*MSG0*/"Autodesk.Aec.BuiltInUnit")
				throw new InvalidOperationException(); // yikes! the "enum" is of the wrong type; something got changed on us?
			return unit.ToString();
		}

		/// <summary>
		/// Get the AEC unit(that is used for measuring, etc) to foot unit ratio 
		/// </summary>
		/// <returns></returns>
		public static double? GetAecUnitToFootRatio(Database acadDatabase)
		{
			Contract.Requires(acadDatabase != null);

            using (var transaction = acadDatabase.TransactionManager.StartOpenCloseTransaction())
			{
                var dictionaryId = GetSubDictionaryFromNod(acadDatabase, AEC_VARS_DICTIONARY_NAME, false);
				if (!dictionaryId.IsNull)
				{
					var aecvarsDict = (DBDictionary)transaction.GetObject(dictionaryId, OpenMode.ForRead);
					if (aecvarsDict.Contains(AEC_VARS_RECORD_NAME))
					{
						ObjectId id = aecvarsDict.GetAt(AEC_VARS_RECORD_NAME);
						var aecvarsDwgSetup = transaction.GetObject(id, OpenMode.ForRead);
						// If the AEC objects aren't loaded (running in vanilla AutoCAD), this will fail.
						if (aecvarsDwgSetup.GetType().FullName != /*MSG0*/"Autodesk.Aec.ApplicationServices.DrawingSetupVariables")
							return null; // no AEC object available

						var strUnit = GetLinearUnitString(aecvarsDwgSetup);
						switch (strUnit)
						{
							case "Foot": return 1.0;
							case "Inch": return 1.0 / 12.0;
							case "Mile": return 5280.0;
							case "Millimeter": return 0.003281;
							case "Centimeter": return 0.03281;
							case "Decimeter": return 0.3281;
							case "Meter": return 3.281;
							case "Kilometer": return 3281.0;
							default: throw new InvalidOperationException();
						}
					}
				}
			}
			return null;
		}

		public static double GetInsunitToFootRatio(Database acadDatabase)
		{
			switch (acadDatabase.Insunits)
			{
				case UnitsValue.Undefined:
				{
					if (acadDatabase.Measurement == MeasurementValue.English)
						return 1.0 / 12.0;
					else if (acadDatabase.Measurement == MeasurementValue.Metric)
						return 0.003281;

					throw new InvalidOperationException();
				}

				case UnitsValue.Inches: return 1.0 / 12.0;
				case UnitsValue.Feet: return 1.0;
				case UnitsValue.Miles: return 5280.0;
				case UnitsValue.Millimeters: return 0.003281;
				case UnitsValue.Centimeters: return 0.03281;
				case UnitsValue.Meters: return 3.281;
				case UnitsValue.Kilometers: return 3281.0;
				case UnitsValue.MicroInches: return 8.333e-8;
				case UnitsValue.Mils: return 8.333e-5;
				case UnitsValue.Yards: return 3.0;
				case UnitsValue.Angstroms: return 3.281e-10;
				case UnitsValue.Nanometers: return 3.281e-9;
				case UnitsValue.Microns: return 3.281e-6;
				case UnitsValue.Decimeters: return 0.3281;
				case UnitsValue.Dekameters: return 32.81;
				case UnitsValue.Hectometers: return 328.1;
				case UnitsValue.Gigameters: return 3.281e9;
				case UnitsValue.Astronomical: return 4.908e11;
				case UnitsValue.LightYears: return 3.1018e16;
				case UnitsValue.Parsecs: return 1.0124e17;

				default:
					throw new InvalidOperationException();
			}
		}

        public static void MoveToBottom(Database db, ObjectIdCollection objsToMove, ObjectId spaceId)
        {
            using (var trans = db.TransactionManager.StartTransaction())
            {
                var modelSpaceId = spaceId;
                if (modelSpaceId.IsNull)
                    modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(db);

                var btrModelSpace = trans.GetObject(modelSpaceId, OpenMode.ForRead) as BlockTableRecord;

                var dot = trans.GetObject(btrModelSpace.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;

                dot.MoveToBottom(objsToMove);

                trans.Commit();
            }
        }

        public static void DisposeNonresidentObjects(DBObjectCollection objectsToDispose)
        {
            foreach (DBObject obj in objectsToDispose)
            {
                obj.Dispose();
            }
        }
	}
}
