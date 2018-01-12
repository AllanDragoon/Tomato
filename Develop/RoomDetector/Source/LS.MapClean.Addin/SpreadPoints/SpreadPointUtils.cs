using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.GraphicsSystem;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.SpreadPoints
{
    class SpreadPointUtils
    {
        #region APIs

        public static IEnumerable<SpreadPoint> ReadSpreadPointsFromFile(string filePath)
        {
            var result = new List<SpreadPoint>();
            // http://stackoverflow.com/questions/8037070/whats-the-fastest-way-to-read-a-text-file-line-by-line
            // For some reason you set the buffer size to the smallest possible value (128). 
            // Increasing this will in general increase performance. The default size is 1,024 
            // and other good choices are 512 (the sector size in Windows) or 4,096 (the cluster size in NTFS). 
            // You will have to run a benchmark to determine an optimal buffer size. 
            // A bigger buffer is - if not faster - at least not slower than a smaller buffer.
            const Int32 bufferSize = 512;
            using (var fileStream = File.OpenRead(filePath))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, bufferSize))
            {
                String line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    // Process line
                    var spreadPoint = ParseSpreadPoint(line);
                    if (spreadPoint != null) { 
                        result.Add(spreadPoint);
                    }
                }
            }
            return result;
        }

        public static IEnumerable<ObjectId> InsertSpreadPoints(Document document, IEnumerable<SpreadPoint> spreadPoints,
            SpreadPointSettings settings)
        {
            var result = new List<ObjectId>();

            // Create text style
            var textStyleId = CreateSpreadPointTextStyle(settings, document.Database);
            // Create layer
            var layerId = CreateSpreadPointLayer(settings, document.Database);

            // Insert points
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(document.Database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);
                foreach (var spreadPoint in spreadPoints)
                {
                    var ids = InsertSpreadPoint(spreadPoint, modelspace, transaction, layerId, textStyleId, settings);
                    result.AddRange(ids);
                }
                transaction.Commit();
            }

            return result;
        }

        public static void UpdateSpreadPoints(Document document, SpreadPointSettings settings)
        {
            // Create text style
            var textStyleId = CreateSpreadPointTextStyle(settings, document.Database);
            // Create layer
            var layerId = CreateSpreadPointLayer(settings, document.Database);

            List<ObjectId> spreadPointIds = new List<ObjectId>();
            // Get all spread point Ids.
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                // Get all spread points
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(document.Database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForRead);
                foreach (ObjectId objId in modelspace)
                {
                    var dbPoint = transaction.GetObject(objId, OpenMode.ForRead) as DBPoint;
                    if (dbPoint == null)
                        continue;
                    if(IsSpreadPoint(dbPoint))
                        spreadPointIds.Add(objId);
                }
                transaction.Commit();
            }

            // Update spread point annotation
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                // Get all spread points
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(document.Database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);

                foreach (var spreadPointId in spreadPointIds)
                {
                    var dbPoint = (DBPoint)transaction.GetObject(spreadPointId, OpenMode.ForRead);
                    var spreadPoint = GetSpreadPoint(dbPoint);
                    var annotationId = GetSpreadPointAnnotationId(dbPoint, transaction);

                    // If the point has annotation.
                    if (!annotationId.IsNull)
                    {
                        var dbText = (DBText)transaction.GetObject(annotationId, OpenMode.ForWrite);
                        if (settings.InsertId)
                        {
                            dbText.TextString = spreadPoint.Name;
                        }
                        else if (settings.InsertCode)
                        {
                            dbText.TextString = spreadPoint.Code;
                        }
                        else // No id, no code
                        {
                            dbText.Erase();
                        }
                    }
                    else if(settings.InsertId || settings.InsertCode)
                    {
                        var text = settings.InsertId ? spreadPoint.Name : spreadPoint.Code;
                        var textId = InsertPointAnnotation(text, spreadPoint.Point, modelspace, transaction, 
                            layerId, textStyleId, settings);
                        var groupId = GetSpreadPointGroupId(dbPoint, transaction);
                        var group = (Group)transaction.GetObject(groupId, OpenMode.ForWrite);
                        group.Append(textId);
                    }
                }
                transaction.Commit();
            }
        }

        #endregion

        #region Utils
        private static ObjectId CreateSpreadPointLayer(SpreadPointSettings settings, Database database)
        {
            ObjectId result = ObjectId.Null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var layerTableId = database.LayerTableId;
                var layerTable = (LayerTable) transaction.GetObject(layerTableId, OpenMode.ForRead);
                if (layerTable.Has(settings.LayerName))
                {
                    result = layerTable[settings.LayerName];
                }
                else
                {
                    layerTable.UpgradeOpen();
                    var layerTableRecord = new LayerTableRecord()
                    {
                        Name = settings.LayerName,
                        Color = settings.LayerColor.ToAcadColor()
                    };
                    result = layerTable.Add(layerTableRecord);
                    transaction.AddNewlyCreatedDBObject(layerTableRecord, add: true);
                }
                transaction.Commit();
            }
            return result;
        }

        private static ObjectId CreateSpreadPointTextStyle(SpreadPointSettings settings, Database database)
        {
            ObjectId result = ObjectId.Null;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var textStyleTable = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
                if (textStyleTable.Has(settings.TextStyleName))
                {
                    result = textStyleTable[settings.TextStyleName];
                }
                else
                {
                    textStyleTable.UpgradeOpen();
                    var textStyleRecord = new TextStyleTableRecord();
                    textStyleRecord.Name = settings.TextStyleName;
                    textStyleRecord.FileName = settings.TextStyleFontFileName;
                    textStyleRecord.BigFontFileName = settings.TextStyleBigFontFileName;
                    result = textStyleTable.Add(textStyleRecord);
                    transaction.AddNewlyCreatedDBObject(textStyleRecord, add: true);
                }
                transaction.Commit();
            }
            return result;
        }

        private const string _nameAppName = "NAME";
        private const string _codeAppName = "CODE";
        private static IEnumerable<ObjectId> InsertSpreadPoint(SpreadPoint spreadPoint, BlockTableRecord space, Transaction transaction,
            ObjectId layerId, ObjectId textStyleId, SpreadPointSettings settings)
        {
            var result = new List<ObjectId>();
            var idCollection = new ObjectIdCollection();

            // Add new point
            var dbPoint = new DBPoint(spreadPoint.Point);
            dbPoint.LayerId = layerId;
            dbPoint.Color = settings.Color.ToAcadColor(); // Color
            var objId = space.AppendEntity(dbPoint);

            // Add xdata
            // Id - compitable with CASS.
            XDataUtils.SetXDataByAppName(layerId.Database, dbPoint, _nameAppName, new object[] { spreadPoint.Name });
            // Code - compitable with CASS
            XDataUtils.SetXDataByAppName(layerId.Database, dbPoint, _codeAppName, new object[] { spreadPoint.Code });

            transaction.AddNewlyCreatedDBObject(dbPoint, add: true);

            idCollection.Add(objId);
            result.Add(objId);

            // Add id text
            if (settings.InsertId)
            {
                var textId = InsertPointAnnotation(spreadPoint.Name, spreadPoint.Point, space, transaction, layerId,
                    textStyleId, settings);

                idCollection.Add(textId);
                result.Add(textId);
            }

            // Add code
            if (settings.InsertCode && !String.IsNullOrEmpty(spreadPoint.Code))
            {
                var codeId = InsertPointAnnotation(spreadPoint.Code, spreadPoint.Point, space, transaction, layerId, 
                    textStyleId, settings);

                idCollection.Add(codeId);
                result.Add(codeId);
            }

            CreateNewGroup(space.Database, "sprdpt", idCollection, transaction);
            return result;
        }

        private static bool IsSpreadPoint(DBPoint dbPoint)
        {
            var nameRb = dbPoint.GetXDataForApplication(_nameAppName);
            var codeRb = dbPoint.GetXDataForApplication(_codeAppName);
            if (nameRb != null && codeRb != null)
                return true;
            return false;
        }

        private static SpreadPoint GetSpreadPoint(DBPoint dbPoint)
        {
            var rbName = dbPoint.GetXDataForApplication(_nameAppName);
            if (rbName == null)
                return null;
            var rbCode = dbPoint.GetXDataForApplication(_codeAppName);
            if (rbCode == null)
                return null;

            var rbNameArray = rbName.AsArray();
            var rbCodeArray = rbCode.AsArray();
            if (rbCodeArray.Length < 2 || rbNameArray.Length < 2)
                return null;

            return new SpreadPoint()
            {
                Point = dbPoint.Position,
                Name = (string)rbNameArray[1].Value,
                Code = (string)rbCodeArray[1].Value
            };
        }

        private static ObjectId InsertPointAnnotation(String text, Point3d basePosition, BlockTableRecord space,
            Transaction transaction,  ObjectId layerId, ObjectId textStyleId, SpreadPointSettings settings)
        {
            var dbText = new DBText();
            dbText.TextString = text;
            dbText.HorizontalMode = TextHorizontalMode.TextLeft;
            dbText.VerticalMode = TextVerticalMode.TextBase;
            dbText.TextStyleId = textStyleId;
            dbText.LayerId = layerId;
            dbText.Color = settings.Color.ToAcadColor();
            dbText.Height = settings.FontHeight * settings.Scale;
            dbText.WidthFactor = 0.8;
            var vector = new Vector3d(settings.AnnotationXOffset * settings.Scale, settings.AnnotationYOffset * settings.Scale, 0.0);
            var position = basePosition + vector;
            dbText.Position = position;
            // If vertical mode is .TextBase, then the position point is used to determine the text's position.
            // The alignment point is recalculated based on the text string and the position point's value.
            // dbText.AlignmentPoint = position;
            var objectId = space.AppendEntity(dbText);
            transaction.AddNewlyCreatedDBObject(dbText, add: true);
            return objectId;
        }

        private static ObjectId GetSpreadPointGroupId(DBPoint dbPoint, Transaction transaction)
        {
            ObjectId resultId = ObjectId.Null;
            var reactorIds = dbPoint.GetPersistentReactorIds();
            foreach (ObjectId reactorId in reactorIds)
            {
                var group = transaction.GetObject(reactorId, OpenMode.ForRead) as Group;
                if (group != null)
                {
                    resultId = reactorId;
                    group.Dispose();
                } 
            }
            return resultId;
        }

        private static ObjectId GetSpreadPointAnnotationId(DBPoint dbPoint, Transaction transaction)
        {
            var groupId = GetSpreadPointGroupId(dbPoint, transaction);
            if (groupId.IsNull)
                return ObjectId.Null;

            ObjectId resultId = ObjectId.Null;
            var group = transaction.GetObject(groupId, OpenMode.ForRead) as Group;
            var entityIds = group.GetAllEntityIds();
            foreach (ObjectId id in entityIds)
            {
                var text = transaction.GetObject(id, OpenMode.ForRead) as DBText;
                if (text != null)
                {
                    resultId = id;
                    text.Dispose();
                    break;
                }
            }
            group.Dispose();
            return resultId;
        }

        private static ObjectId CreateNewGroup(Database db, string grpPattern, 
            ObjectIdCollection ids, Transaction tr)
        {
            // Get the group dictionary from the drawing
            var gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);

            // Check the group name, to see whether it's already in use
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
            return grpId;
        }

        private static SpreadPoint ParseSpreadPoint(string textLine)
        {
            if (String.IsNullOrEmpty(textLine))
                return null;
            var array = textLine.Split(',');
            // TODO: always 5?
            if (array.Length < 4)
                return null;

            double? x, y;
            string name = array[0];
            string code = array[1];

            double tempd;
            if (double.TryParse(array[2], out tempd))
                y = tempd;
            else
                return null;

            if (double.TryParse(array[3], out tempd))
                x = tempd;
            else
                return null;
            return new SpreadPoint()
            {
                Name = name,
                Code = code,
                Point = new Point3d(x.Value, y.Value, 0.0)
            };
        }
        #endregion
    }
}
