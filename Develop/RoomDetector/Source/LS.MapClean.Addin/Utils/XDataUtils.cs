using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace LS.MapClean.Addin.Utils
{
    class XDataUtils
    {
        public static List<object> ReadXDataByAppName(DBObject dbObject, string appName)
        {
            var attribute = new List<object>();
            try
            {
                var rb = dbObject.GetXDataForApplication(appName);
                if (rb != null)
                {
                    var rvArr = rb.AsArray();
                    if (rvArr.Count() >= 2)
                    {
                        // XData of appliation name (1001)
                        if ((DxfCode)rvArr[0].TypeCode == DxfCode.ExtendedDataRegAppName
                            && rvArr[0].Value.ToString().ToUpper().Trim() == appName.ToUpper())
                        {
                            for (var i = 1; i < rvArr.Length; i++)
                            {
                                var typedValue = rvArr[i];
                                attribute.Add(typedValue.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                MessageBox.Show(@"读取xdata有错误", @"建库软件", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            return attribute;
        }

        public static void SetXDataByAppName(Database database, DBObject dbObject, string appName, object[] values)
        {
            AddRegAppTableRecord(database, appName);

            var i = 0;
            // Write reg app name
            var typedValues = new TypedValue[values.Length + 1];
            typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName);
            foreach (var value in values)
            {
                if (value == null)
                    typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataAsciiString, String.Empty);
                else if (value is string)
                    typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataAsciiString, value);
                else if (value is double || value is decimal || value is float)
                    typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataReal, value);
                else if (value is long)
                    typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataInteger32, value);
                // Storing and retrieving handles from a resbuf using the .NET API
                // http://adndevblog.typepad.com/autocad/2012/06/storing-and-retrieving-handles-from-a-resbuf-using-the-net-api.html
                else if (value is ObjectId)
                    typedValues[i++] = new TypedValue((int)DxfCode.ExtendedDataAsciiString, ((ObjectId)value).Handle.Value.ToString());
                else
                    throw new InvalidOperationException("data type not support yet");
            }

            using (var rb = new ResultBuffer(typedValues))
            {
                dbObject.XData = rb;
            }
        }

        public static void AddRegAppTableRecord(Database database, string regAppName)
        {
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var rat = (RegAppTable)tr.GetObject(database.RegAppTableId, OpenMode.ForRead, false);

                if (!rat.Has(regAppName))
                {
                    rat.UpgradeOpen();
                    var ratr = new RegAppTableRecord { Name = regAppName };
                    rat.Add(ratr);
                    tr.AddNewlyCreatedDBObject(ratr, true);
                }

                tr.Commit();
            }
        }
    }
}
