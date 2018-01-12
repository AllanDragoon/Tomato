using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace LS.MapClean.Addin.Utils
{
    public static class SelectionFilterUtils
    {
        /// <summary>
        /// Only select curve: So far includes Line, PolyLine, Polyline2d.
        /// </summary>
        /// <returns></returns>
        public static SelectionFilter OnlySelectCurve()
        {
            var filList = new TypedValue[6];
            filList[0] = new TypedValue((int)DxfCode.Operator, "<or");
            filList[1] = new TypedValue((int)DxfCode.Start, "LWPOLYLINE");
            filList[2] = new TypedValue((int)DxfCode.Start, "POLYLINE");
            filList[3] = new TypedValue((int)DxfCode.Start, "LINE");
            filList[4] = new TypedValue((int)DxfCode.Start, "ARC");
            filList[5] = new TypedValue((int)DxfCode.Operator, "or>");
            var filter = new SelectionFilter(filList);
            return filter;
        }

        /// <summary>
        /// Only select polyline.
        /// </summary>
        /// <returns></returns>
        public static SelectionFilter OnlySelectPolyline()
        {
            var filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "POLYLINE")
            });
            return filter;
        }

        /// <summary>
        /// Only select polyline and polyline2d
        /// </summary>
        /// <returns></returns>
        public static SelectionFilter OnlySelectPolylineAndPolyline2d()
        {
            var filList = new TypedValue[4];
            filList[0] = new TypedValue((int)DxfCode.Operator, "<or");
            filList[1] = new TypedValue((int)DxfCode.Start, "LWPOLYLINE");
            filList[2] = new TypedValue((int)DxfCode.Start, "POLYLINE");
            filList[3] = new TypedValue((int)DxfCode.Operator, "or>");
            var filter = new SelectionFilter(filList);
            return filter;
        }
    }
}
