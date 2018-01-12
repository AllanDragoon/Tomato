using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Algorithms
{
    public enum ExtendType
    {
        None,
        ExtendStart,
        ExtendEnd
    }

    /// <summary>
    /// ApparentIntersection data struct
    /// </summary>
    public class IntersectionInfo
    {
        public IntersectionInfo(ExtendType sourceExtendType, ExtendType targetExtendType, Point3d intersectPoint)
        {
            _sourceExtendType = sourceExtendType;
            _targetExtendType = targetExtendType;
            _intersectPoint = intersectPoint;
        }

        public IntersectionInfo(ObjectId sourceId, ExtendType sourceExtendType, ObjectId targetId, ExtendType targetExtendType, Point3d intersectPoint)
        {
            SourceId = sourceId;
            _sourceExtendType = sourceExtendType;

            TargetId = targetId;
            _targetExtendType = targetExtendType;

            _intersectPoint = intersectPoint;
        }

        private readonly Point3d _intersectPoint;
        public Point3d IntersectPoint
        {
            get { return _intersectPoint; }
        }

        public ObjectId SourceId { get; set; }

        private readonly ExtendType _sourceExtendType;
        public ExtendType SourceExtendType
        {
            get { return _sourceExtendType; }
        }

        public ObjectId TargetId { get; set; }

        private readonly ExtendType _targetExtendType;
        public ExtendType TargetExtendType
        {
            get { return _targetExtendType; }
        }
    }
}
