using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Operation.Valid;
using TopologyTools.ReaderWriter;

namespace TopologyTools.Utils
{
    public enum SingleTopologyErrors
    {
        [Obsolete("Not used")]
        Error = 0,
        [Obsolete("No longer used: repeated points are considered valid as per the SFS")]
        RepeatedPoint = 1,
        HoleOutsideShell = 2,
        NestedHoles = 3,
        DisconnectedInteriors = 4,
        SelfIntersection = 5,
        RingSelfIntersection = 6,
        NestedShells = 7,
        DuplicateRings = 8,
        TooFewPoints = 9,
        InvalidCoordinate = 10,
        RingNotClosed = 11,
    }

    public class SingleTopologyError
    {
        public SingleTopologyError(SingleTopologyErrors errorType)
            : this(errorType, Point3d.Origin)
        {
            
        }

        public SingleTopologyError(SingleTopologyErrors errorType, Point3d pt)
        {
            Coordinate = pt;
            ErrorType = errorType;
        }

        public Point3d Coordinate { get; set; }

        public SingleTopologyErrors ErrorType {get; set;}

        public string Message
        {
            get
            {
                if (ErrorType == SingleTopologyErrors.RepeatedPoint)
                    return "重复点";
                if (ErrorType == SingleTopologyErrors.HoleOutsideShell)
                    return "外部洞";
                if (ErrorType == SingleTopologyErrors.NestedHoles)
                    return "嵌套洞";
                if (ErrorType == SingleTopologyErrors.DisconnectedInteriors)
                    return "内部边界不联通";
                if (ErrorType == SingleTopologyErrors.SelfIntersection)
                    return "自交";
                if (ErrorType == SingleTopologyErrors.RingSelfIntersection)
                    return "环自交";
                if (ErrorType == SingleTopologyErrors.NestedShells)
                    return "嵌套外壳";
                if (ErrorType == SingleTopologyErrors.DuplicateRings)
                    return "环重复";
                if (ErrorType == SingleTopologyErrors.InvalidCoordinate)
                    return "坐标点无效";
                if (ErrorType == SingleTopologyErrors.RingNotClosed)
                    return "环不封闭";
                return "错误";
            }
        }
    }

    public class SingleTopologyValidator
    {
        public static void ShowSelfIntersections(ILineString line)
        {
            Console.WriteLine("Line: " + line);
            Console.WriteLine("Self Intersections: " + LineStringSelfIntersectionsOp(line));
        }

        //public static IList<ObjectId> CheckValid(IList<ObjectId> objectIds)
        //{
        //    var result = IsValid(objectIds);
        //    System.Diagnostics.Trace.WriteLine(result.Count);
        //    return result.Keys.ToList();
        //}

        public static Dictionary<ObjectId, SingleTopologyError> CheckValid(IList<ObjectId> objectIds)
        {
            var errorDictionary = new Dictionary<ObjectId, SingleTopologyError>();
            if (!objectIds.Any())
                return errorDictionary;

            var database = objectIds[0].Database;

            var geometries = new List<IGeometry>();
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                foreach (ObjectId objectId in objectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    IGeometry geom = reader.ReadEntityAsGeometry(tr, objectId);
                    if (geom == null)
                        continue;
                    
                    geometries.Add(geom);
                    if (!geom.IsValid)
                    {
                        var ivop = new IsValidOp(geom);
                        if (!ivop.IsValid)
                        {
                            Console.WriteLine(geom.AsText());
                            Console.Write(ivop.ValidationError);
                            int errorType = (int) ivop.ValidationError.ErrorType;
                            var point = new Point3d(ivop.ValidationError.Coordinate.X, ivop.ValidationError.Coordinate.Y, 0);
                            var error = new SingleTopologyError((SingleTopologyErrors)errorType, point);
                            errorDictionary.Add(objectId, error);
                        }
                    }
                }
                tr.Commit();
            }

            return errorDictionary;
        }

        public static IList<Point3d> LineStringSelfIntersectionsOp(ObjectId objectId)
        {
            var result = LineStringSelfIntersectionsOp(new List<ObjectId>() {objectId});
            return result.ContainsKey(objectId) ?  result[objectId] : new List<Point3d>();
        }

        public static Dictionary<ObjectId, IList<Point3d>> LineStringSelfIntersectionsOp(IList<ObjectId> objectIds)
        {
            var dictionary = new Dictionary<ObjectId, IList<Point3d>>();

            var database = objectIds[0].Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                foreach (ObjectId objectId in objectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    var curve = tr.GetObject(objectId, OpenMode.ForRead) as Curve;
                    var geom = reader.ReadCurveAsLineString(tr, curve) as LineString;
                    var intersectGeom = LineStringSelfIntersectionsOp(geom);
                    
                    var points = new List<Point3d>();
                    foreach (var coordinate in intersectGeom.Coordinates)
                    {
                        // 如果是NaN直接设定为0
                        if (double.IsNaN(coordinate.Z))
                            coordinate.Z = 0;

                        points.Add(new Point3d(coordinate.X, coordinate.Y, coordinate.Z));
                    }
                    dictionary.Add(objectId, points);
                }
                tr.Commit();
            }

            return dictionary;
        }

        public static Dictionary<ObjectId, IList<Point3d>> FindDanglingLine(IList<ObjectId> objectIds)
        {
            var dictionary = new Dictionary<ObjectId, IList<Point3d>>();
            //var points = new List<Point3d>();
            var database = objectIds[0].Database;
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var reader = new DwgReader();
                // var pmFixed3 = new PrecisionModel(3);
                // 读入多边形数据
                foreach (ObjectId objectId in objectIds)
                {
                    if (!objectId.IsValid)
                        continue;

                    IGeometry geom = reader.ReadEntityAsGeometry(tr, objectId);
                    if (geom == null)
                        continue;

                    // 开始做Union
                    var nodedLineString = UnaryUnionOp.Union(geom);
                    var polygonizer = new Polygonizer();
                    polygonizer.Add(nodedLineString);
                    var dangles = polygonizer.GetDangles();

                    // 悬挂线
                    var points = new List<Point3d>();
                    foreach (ILineString lineString in dangles)
                    {
                        foreach (var coordinate in lineString.Coordinates)
                        {
                            // 如果是NaN直接设定为0
                            if (double.IsNaN(coordinate.Z))
                                coordinate.Z = 0;

                            points.Add(new Point3d(coordinate.X, coordinate.Y, coordinate.Z));
                        }
                    }
                    if (points.Any())
                        dictionary.Add(objectId, points);
                }
                tr.Commit();
            }

            return dictionary;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        static IGeometry LineStringSelfIntersectionsOp(ILineString line)
        {
            IGeometry lineEndPts = GetEndPoints(line);
            IGeometry nodedLine = line.Union(lineEndPts);
            IGeometry nodedEndPts = GetEndPoints(nodedLine);
            IGeometry selfIntersections = nodedEndPts.Difference(lineEndPts);
            return selfIntersections;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        static IGeometry GetEndPoints(IGeometry g)
        {
            List<Coordinate> endPtList = new List<Coordinate>();
            if (g is ILineString)
            {
                ILineString line = (ILineString)g;
                endPtList.Add(line.GetCoordinateN(0));
                endPtList.Add(line.GetCoordinateN(line.NumPoints - 1));
            }
            else if (g is IMultiLineString)
            {
                IMultiLineString mls = (IMultiLineString)g;
                for (int i = 0; i < mls.NumGeometries; i++)
                {
                    ILineString line = (ILineString)mls.GetGeometryN(i);
                    endPtList.Add(line.GetCoordinateN(0));
                    endPtList.Add(line.GetCoordinateN(line.NumPoints - 1));
                }
            }
            Coordinate[] endPts = endPtList.ToArray();
            return GeometryFactory.Default.CreateMultiPoint(endPts);
        }
    }
}
