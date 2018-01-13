using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.Algorithms
{
    public class LinkedPoint
    {
        private LinkedPoint _next;
        private LinkedPoint _prev;

        //public LinkArxPoint()
        //{
        //    OriginPoint = Point3d.Origin;
        //    RotatePoint = Point3d.Origin;
        //}

        public LinkedPoint(Point3d pt)
        {
            Point = pt;
        }

        public Point3d Point { get; set; }

        public LinkedPoint Next
        {
            get { return _next; }
            set
            {
                _next = value;
                if (_next != null)
                    _next._prev = this;
            }
        }

        public LinkedPoint Prev
        {
            get { return _prev; }
            set
            {
                _prev = value;
                if (_prev != null)
                    _prev.Next = this;
            }
        }

        /// <summary>
        /// Build a linked list for vertices.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="transaction"></param>
        /// <param name="isLoop">Indicate whether the curve is a loop</param>
        /// <returns></returns>
        public static LinkedPoint GetLinkedPoints(Curve curve, Transaction transaction, bool isLoop)
        {
            var vertices = CurveUtils.GetDistinctVertices(curve, transaction);
            // Make sure the first point is not equal to the last one.
            if (vertices.Count > 1 && vertices[0] == vertices[vertices.Count - 1])
                vertices.RemoveAt(vertices.Count - 1);

            return GetLinkedPoints(vertices, isLoop);
        }

        /// <summary>
        /// Build a linked list for vertices.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="isLoop">Indicate whether the curve is a loop</param>
        /// <returns></returns>
        public static LinkedPoint GetLinkedPoints(List<Point3d> vertices, bool isLoop)
        {
            LinkedPoint ptLink = null;
            LinkedPoint prevPt = null;
            foreach (var point3D in vertices)
            {
                // Rotated point is useless here.
                var tempLinkPt = new LinkedPoint(point3D);
                if (prevPt != null)
                {
                    tempLinkPt.Prev = prevPt;
                }
                else
                {
                    ptLink = tempLinkPt;
                }
                prevPt = tempLinkPt;
            }

            // NOTE: Let it to be a closed list
            // prevPt is the last point
            if (prevPt != null && isLoop)
                prevPt.Next = ptLink;
            return ptLink;
        }
    }

    internal class CurveLinkedPoint
    {
        public CurveLinkedPoint(LinkedPoint linkedPoint, ObjectId id)
        {
            LinkedPoint = linkedPoint;
            ObjectId = id;
        }

        public ObjectId ObjectId { get; private set; }
        public LinkedPoint LinkedPoint { get; private set; }
    }
}
