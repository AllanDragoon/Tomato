using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Utils;
using TopologyTools.GeometryExtensions;

namespace LS.MapClean.Addin.MapClean
{
    public class ArcSegmentAction : MapCleanActionBase
    {
        public ArcSegmentAction(Document document)
            : base(document)
        {
        }

        public override ActionType ActionType
        {
            get { return ActionType.ArcSegment; }
        }

        public override bool Hasparameters
        {
            get { return true; }
        }

        protected override IEnumerable<CheckResult> CheckImpl(IEnumerable<ObjectId> selectedObjectIds)
        {
            var result = new List<ArcSegmentCheckResult>();
            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                foreach (var selectedObjectId in selectedObjectIds)
                {
                    List<Curve> arcs = null;
                    if (!IsOrHasArc(selectedObjectId, transaction, out arcs))
                        continue;
                    var checkResult = new ArcSegmentCheckResult(selectedObjectId, arcs.ToArray());
                    result.Add(checkResult);
                }
                transaction.Commit();
            }
            return result;
        }

        protected override Status FixImpl(CheckResult checkResult, out List<ObjectId> resultIds)
        {
            resultIds = new List<ObjectId>();
            var arcsegmentResult = checkResult as ArcSegmentCheckResult;
            if (arcsegmentResult == null || !arcsegmentResult.SourceIds.Any())
                return Status.Invalid;

            var curveId = arcsegmentResult.SourceIds.First();
            using (var transaction = curveId.Database.TransactionManager.StartTransaction())
            {
                var entity = transaction.GetObject(curveId, OpenMode.ForRead);
                if (entity is Arc || entity is Circle || entity is Ellipse)
                {
                    entity.UpgradeOpen();
                    entity.Erase();
                }
                else if (entity is Polyline)
                {
                    entity.UpgradeOpen();
                    StraigthenPolyline((Polyline) entity);
                }
                else if (entity is Polyline2d)
                {
                    StraigthenPolyline2d((Polyline2d) entity, transaction);
                }
                transaction.Commit();
            }
            return Status.Fixed;
        }

        private bool IsOrHasArc(ObjectId objectId, Transaction transaction, out List<Curve> arcs)
        {
            arcs = new List<Curve>();
            var curve = transaction.GetObject(objectId, OpenMode.ForRead) as Curve;
            if (curve == null)
                return false;

            if (curve is Arc || curve is Circle || curve is Ellipse)
            {
                arcs.Add((Curve)curve.Clone());
            }
            else if (curve is Polyline)
            {
                // http://www.afralisp.net/autolisp/tutorials/polyline-bulges-part-1.php
                // Use bulge to determine whether the segment is arc or not
                var polyline = (Polyline) curve;
                var verticesNum = polyline.NumberOfVertices;
                for (int i = 0; i < verticesNum; i++)
                {
                    // The bulge is the tangent of 1/4 of the included angle for the arc 
                    // between the selected vertex and the next vertex in the polyline's vertex list.
                    // A negative bulge value indicates that the arc goes clockwise 
                    // from the selected vertex to the next vertex. 
                    // A bulge of 0 indicates a straight segment, and a bulge of 1 is a semicircle.
                    var segmentType = polyline.GetSegmentType(i);
                    if (segmentType != SegmentType.Arc)
                        continue;
                    
                    // Use GetArcSegmentAt2 to avoid crash  - sometimes if arc segment's two end points are nearly equal,
                    // eInvalidInput will be thrown.
                    var arcSegment = polyline.GetArcSegmentAt2(i);

                    // http://forums.autodesk.com/t5/net/create-arc-with-startpoint-endpoint-radius/td-p/3772148
                    // CircularArc3d to Arc
                    double angle = arcSegment.ReferenceVector.AngleOnPlane(new Plane(arcSegment.Center, arcSegment.Normal));
                    var arc = new Arc(arcSegment.Center, arcSegment.Normal, arcSegment.Radius, arcSegment.StartAngle + angle, arcSegment.EndAngle + angle);
                    arcs.Add(arc);
                }
            }
            else if (curve is Polyline2d)
            {
                var polyline2D = (Polyline2d) curve;
                int index = 0;
                foreach (ObjectId id in polyline2D)
                {
                    var vertex2D = transaction.GetObject(id, OpenMode.ForRead) as Vertex2d;
                    if (vertex2D != null && !vertex2D.Bulge.EqualsWithTolerance(0.0))
                    {
                        var arcSegment = polyline2D.GetArcSegmentAt(index);
                        // http://forums.autodesk.com/t5/net/create-arc-with-startpoint-endpoint-radius/td-p/3772148
                        // CircularArc3d to Arc
                        double angle = arcSegment.ReferenceVector.AngleOnPlane(new Plane(arcSegment.Center, arcSegment.Normal));
                        var arc = new Arc(arcSegment.Center, arcSegment.Normal, arcSegment.Radius, arcSegment.StartAngle + angle, arcSegment.EndAngle + angle);
                        arcs.Add(arc);
                    }
                    index++;
                }
            }
            return arcs.Count > 0;;
        }

        private bool StraigthenPolyline(Polyline polyline)
        {
            var verticesNum = polyline.NumberOfVertices;
            for (int i = 0; i < verticesNum; i++)
            {
                var bulge = polyline.GetBulgeAt(i);
                if (bulge.EqualsWithTolerance(0.0))
                    continue;
                polyline.SetBulgeAt(i, 0.0);
            }
            return true;
        }

        private bool StraigthenPolyline2d(Polyline2d polyline2D, Transaction transaction)
        {
            foreach (ObjectId id in polyline2D)
            {
                var vertex2D = transaction.GetObject(id, OpenMode.ForRead) as Vertex2d;
                if (vertex2D != null && !vertex2D.Bulge.EqualsWithTolerance(0.0))
                {
                    vertex2D.UpgradeOpen();
                    vertex2D.Bulge = 0.0;
                }
            }
            return true;
        }
    }
}
