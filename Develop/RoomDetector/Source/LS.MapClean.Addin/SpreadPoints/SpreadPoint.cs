using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Geometry;

namespace LS.MapClean.Addin.SpreadPoints
{
    /// <summary>
    /// Spread point info.
    /// </summary>
    class SpreadPoint
    {
        /// <summary>
        /// The coordinates of a spread point
        /// </summary>
        public Point3d Point { get; set; }

        /// <summary>
        /// The id of spread point.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The code of spread point
        /// </summary>
        public string Code { get; set; }
    }
}
