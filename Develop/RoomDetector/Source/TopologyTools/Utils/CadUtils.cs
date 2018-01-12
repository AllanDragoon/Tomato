using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

namespace TopologyTools.Utils
{
    public static class CadUtils
    {
        public static void DrawPoint(Transaction tr, Database database, DBPoint dbPt, int colorIndex = 1)
        {
            var mode = (short) Application.GetSystemVariable("pdmode");
            if (mode == 0)
                Application.SetSystemVariable("pdmode", 99);
            dbPt.ColorIndex = colorIndex;

            // 输出到CAD
            AddToCurrentDb(tr, database, dbPt);
        }

        public static void DrawPoint(Transaction tr, Database database, Point3d point3D, int colorIndex = 1)
        {
            using (var dbPt = new DBPoint(point3D))
            {
                DrawPoint(tr, database, dbPt, colorIndex);
            }
        }

        public static void DrawText(Transaction tr, Database database, Point3d point3D, string content)
        {
            // Add the MText
            var mText = new MText
            {
                Contents = content,
                Location = point3D,
                Attachment = AttachmentPoint.MiddleCenter,
                TextHeight = 2
            };
            mText.SetDatabaseDefaults();
            AddToCurrentDb(tr, database, mText);
        }

        public static void AddToCurrentDb(Transaction tr, Database database, Entity entity)
        {
            var modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
            var blockTableRecord = (BlockTableRecord) tr.GetObject(modelSpaceId, OpenMode.ForWrite, false);
            blockTableRecord.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
        }

        public static ObjectId[] FindAllPolylines(Document document)
        {
            Editor editor = document.Editor;
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int) DxfCode.Operator, "<or"),
                new TypedValue((int) DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int) DxfCode.Start, "POLYLINE"),
                new TypedValue((int) DxfCode.Operator, "or>")
            });

            PromptSelectionResult promptSelectionResult = editor.SelectAll(filter);
            SelectionSet value = promptSelectionResult.Value;
            if (value != null)
            {
                ObjectId[] array = value.GetObjectIds();
                return array;
            }
            return new ObjectId[0];
        }

        public static double GetParcelArea(ObjectId objectId)
        {
            double area;
            using (var transaction = objectId.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)transaction.GetObject(objectId, OpenMode.ForRead);
                area = GetParcelArea(entity);
                transaction.Commit();
            }
            if (Double.IsNaN(area))
                return 0;
            return area;
        }

        public static double GetParcelArea(Entity polyline)
        {
            var parcelPolyline = polyline as Polyline;
            if (parcelPolyline != null)
            {
                return parcelPolyline.Area;
            }
            var parcelPolyline2D = polyline as Polyline2d;
            if (parcelPolyline2D != null)
            {
                return parcelPolyline2D.Area;
            }

            return 0;
        }

        public static Extents3d? SafeGetGeometricExtents(IList<ObjectId> entityIds)
        {
            Extents3d? result = null;
            using (var transaction = entityIds[0].Database.TransactionManager.StartTransaction())
            {
                var first = true;
                foreach (ObjectId objId in entityIds)
                {
                    if (!objId.IsValid)
                        continue;

                    var entity = transaction.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        // 如果有图形几何是NullExtent，直接skip掉
                        var extents = SafeGetGeometricExtents(entity);
                        if (extents == null)
                            continue;

                        if (first)
                        {
                            result = extents.Value;
                            first = false;
                        }
                        else
                        {
                            result.Value.AddExtents(extents.Value);
                        }
                    }
                }
            }

            return result;
        }

        public static Extents3d? SafeGetGeometricExtents(Entity entity)
        {
            // http://adndevblog.typepad.com/autocad/2012/12/entitygeometricextents-throws-an-exception-enullextents.html
            // Issue
            // When I calculate the extents of entities in a drawing, for some entities an exception is thrown with the "eNullExtents" message. 
            // What is wrong?
            // Solution
            // This exception occurs for an insert of an empty block or for an empty block attribute. 
            // It is "as designed", it's just a notification to the developer about an empty object. 
            // An easy solution is to add a separate catch block for this particular exception:
            var extents = new Extents3d();
            try
            {
                extents = entity.GeometricExtents;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                // The entity is empty and has no extents
                if (ex.Message == "eNullExtents" || ex.Message == "eInvalidExtents")
                {
                    // TODO. We can simply skip this entity...
                    return null;
                }
            }

            return extents;
        }

        /// <summary>
        /// http://through-the-interface.typepad.com/through_the_interface/2012/01/testing-whether-a-point-is-on-any-autocad-curve-using-net.html
        /// A generalised IsPointOnCurve function that works on all
        /// types of Curve (including Polylines), and checks the position
        /// of the returned point rather than relying on catching an exception
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static bool IsPointOnCurveGCP(Curve cv, Point3d pt)
        {
            try
            {
                // Return true if operation succeeds
                Point3d p = cv.GetClosestPointTo(pt, false);
                return (p - pt).Length <= Tolerance.Global.EqualPoint;
            }
            catch { }

            // Otherwise we return false
            return false;
        }

        public static Point3d SafeGetPointAtParameter(Curve curve, double parameter)
        {
            // In some circumstances, point a or point b is not on the curve. So we near to "project" to point to the curve and them compare.
            Point3d point = Point3d.Origin;
            try
            {
                point = curve.GetPointAtParameter(parameter);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            return point;
        }

        public static double SafeGetParameterAtPoint(Curve curve, Point3d a)
        {
            // In some circumstances, point a or point b is not on the curve. So we near to "project" to point to the curve and them compare.
            double param = 0.0;
            try
            {
                try
                {
                    param = curve.GetParameterAtPoint(a);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    Point3d a1 = curve.GetClosestPointTo(a, false);
                    param = curve.GetParameterAtPoint(a1);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }

            return param;
        }

        /// <summary>
        /// Zoom using a view object
        /// </summary>
        /// <param name="ed"></param>
        /// <param name="ext"></param>
        /// <param name="factor"></param>
        public static void ZoomToWin1(this Editor ed, Extents3d ext, double factor = 1.0)
        {
            var min2D = new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
            var max2D = new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y);
            using (var view = new ViewTableRecord())
            {
                view.CenterPoint = min2D + ((max2D - min2D) / 2.0);
                view.Height = (max2D.Y - min2D.Y) * factor;
                view.Width = (max2D.X - min2D.X) * factor;
                ed.SetCurrentView(view);
            }
        }

        public static ObjectId AddName(Database database, Transaction tr, Entity entity, string name)
        {
            ObjectId textId = ObjectId.Null;
            //ObjectId layerId = LayerUtils.AddNewLayer(objectId.Database, LayerNames.NameAnnotation);
            var exts = SafeGetGeometricExtents(entity);
            if (exts != null)
            {
                var txtPosition =
                    new Point3d((exts.Value.MinPoint.X + exts.Value.MaxPoint.X)/2,
                        (exts.Value.MinPoint.Y + exts.Value.MaxPoint.Y)/2, 0);

                var mText = new MText
                {
                    Contents = name,
                    Location = txtPosition,
                    //LayerId = layerId,
                    Attachment = AttachmentPoint.MiddleCenter
                };
                mText.SetDatabaseDefaults();
                AddToCurrentDb(tr, database, mText);
                textId = mText.ObjectId;
            }
            return textId;
        }

        /// <summary>
        /// 利用CAD IntersectWith API获得curve1和curve2的交点。在取得交点之前先将curve1和curve2移动到原点附近，
        /// 这样可以保证不会因为精度问题导致交点计算错误。
        /// </summary>
        /// <param name="database"></param>
        /// <param name="curveId1"></param>
        /// <param name="curveId2"></param>
        /// <returns></returns>
        public static Point3dCollection IntersectWith(Database database, ObjectId curveId1, ObjectId curveId2)
        {
            var points = new Point3dCollection();
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var curveA = tr.GetObject(curveId1, OpenMode.ForRead) as Curve;
                var curveB = tr.GetObject(curveId2, OpenMode.ForRead) as Curve;
                if (curveA == null || curveB == null)
                    return points;

                // 修复bug，这里以前是OpenMode.ForWrite打开的，然后在外部lock的时候，有时候会出错eLockViolation
                // 因为我们只是想比较准确的计算顶点，不是真的想位移
                // 所以，这里咱们就Clone一把后计算
                using (var curve1 = curveA.Clone() as Curve)
                using (var curve2 = curveB.Clone() as Curve)
                {
                    // 将curve1和curve2移动到原点附件，以保证精度问题不会影响结果
                    var matrix = Matrix3d.Displacement(new Point3d(0, 0, 0) - curve1.StartPoint);
                    var inverseMatrix = matrix.Inverse();

                    curve1.TransformBy(matrix);
                    curve2.TransformBy(matrix);

                    curve1.IntersectWith(curve2, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);

                    for (int i = 0; i < points.Count; i++)
                    {
                        points[i] = points[i].TransformBy(inverseMatrix);
                    }
                }
                tr.Abort();
            }

            return points;
        }

        public class RegionIntersection
        {
            public RegionIntersection()
            {
                
            }

            public Region Region { get; set; }
            public ObjectId ObjectId1 { get; set; }
            public ObjectId ObjectId2 { get; set; }
        }

        // 重复性代码，需要删除
        public static RegionIntersection GetIntersectionPart(ObjectId curveId1, ObjectId curveId2)
        {
            var database = curveId1.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var curve1 = tr.GetObject(curveId1, OpenMode.ForRead) as Curve;
                var curve2 = tr.GetObject(curveId2, OpenMode.ForRead) as Curve;
                tr.Commit();
                return GetIntersectionPart(curve1, curve2);
            }
        }

        public static RegionIntersection GetIntersectionPart(Curve curve1, Curve curve2)
        {
            var tmpCur1 = curve1.Clone() as Curve;
            Region region1 = ConstructRegionWithPolyline(tmpCur1);
            tmpCur1.Dispose();

            if (region1 == null)
            {
                return new RegionIntersection() { ObjectId1 = curve1.ObjectId };
            }

            var tmpCur2 = curve2.Clone() as Curve;
            Region region2 = ConstructRegionWithPolyline(tmpCur2);
            tmpCur2.Dispose();

            if (region2 == null)
            {
                region1.Dispose();
                return new RegionIntersection() { ObjectId1 = curve2.ObjectId };
            }

            // Dispose region2
            try
            {
                region1.BooleanOperation(BooleanOperationType.BoolIntersect, region2);
                region2.Dispose();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.GeneralModelingFailure)
                    return new RegionIntersection() { ObjectId1 = curve1.ObjectId, ObjectId2 = curve2.ObjectId };
            }

            if (Math.Abs(region1.Area) > 0)
                return new RegionIntersection() { Region = region1 };

            // Dispose region1
            region1.Dispose();

            // 直接返回了
            return null;
        }

        public static Region ConstructRegionWithPolyline(Curve curve)
        {
            try
            {
                var regions = Region.CreateFromCurves(new DBObjectCollection() { curve });
                if (regions == null || regions.Count == 0)
                    return null;

                var result = regions[0] as Region;
                foreach (DBObject dbObj in regions)
                {
                    if (dbObj != result)
                    {
                        dbObj.Dispose();
                    }
                }

                return result;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                return null;
            }
        }

        public static void SetCassFlag(DBObject dbObject, string flag)
        {
            SetXDataByAppName(dbObject.Database, dbObject, "SOUTH", new object[] { flag });
        }

        public static string GetCassFlag(DBObject dbObject)
        {
            var attributes = ReadXDataByAppName(dbObject, "SOUTH");
            if (attributes != null && attributes.Count >= 1)
                return attributes[0].ToString();
            return String.Empty;
        }

        public static List<object> ReadXDataByAppName(DBObject dbObject, string appName)
        {
            var attribute = new List<object>();
            try
            {
                var rb = dbObject.GetXDataForApplication(appName);
                if (rb != null)
                {
                    var rvArr = rb.AsArray();
                    if (rvArr.Length >= 2)
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

    public class FindOverlap
    {
        Dictionary<ObjectId, Region> regions = new Dictionary<ObjectId, Region>();
        List<Region> resultRegions = new List<Region>();
        public FindOverlap()
        {

        }

        public Region GetIntersectionPart(ObjectId curveId1, ObjectId curveId2)
        {
            var database = curveId1.Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var curve1 = tr.GetObject(curveId1, OpenMode.ForRead) as Curve;
                var curve2 = tr.GetObject(curveId2, OpenMode.ForRead) as Curve;
                return GetIntersectionPart(curve1, curve2);
            }
        }

        public void ReleaseRegions()
        {
            foreach (var region in regions.Values)
            {
                if (region != null)
                    region.Dispose();
            }
            foreach (var region in resultRegions)
            {
                if (region != null)
                    region.Dispose();
            }
        }

        public Region GetIntersectionPart(Curve curve1, Curve curve2)
        {
            Region region1 = GetOrCreateRegion(curve1);
            Region region2 = GetOrCreateRegion(curve2);
            if (region1 != null && region2 != null)
            {
                var cmpRegion = region1.Clone() as Region;
                cmpRegion.BooleanOperation(BooleanOperationType.BoolIntersect, region2);
                resultRegions.Add(cmpRegion);
                if (Math.Abs(cmpRegion.Area) > 0)
                    return cmpRegion;
            }

            return null;
        }

        public Region GetOrCreateRegion(Curve curve)
        {
            Region region;
            if (regions.ContainsKey(curve.ObjectId))
                region = regions[curve.ObjectId];
            else
            {
                region = ConstructRegionWithPolyline(curve);
                regions[curve.ObjectId] = region;
            }

            return region;
        }

        public static Region ConstructRegionWithPolyline(Curve curve)
        {
            try
            {
                using (var tmpCur = curve.Clone() as Curve)
                {
                    var regions = Region.CreateFromCurves(new DBObjectCollection() { tmpCur });
                    if (regions == null || regions.Count == 0)
                        return null;

                    var result = regions[0] as Region;
                    // 如果是有多个region，留第一个，把其他的Dispose
                    foreach (DBObject dbObj in regions)
                    {
                        if (dbObj != result)
                        {
                            dbObj.Dispose();
                        }
                    }

                    return result;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                return null;
            }
        }
    }
}
