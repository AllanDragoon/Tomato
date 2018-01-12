using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;

namespace LS.MapClean.Addin.Utils
{
    class MapScaleUtils
    {
        public static double? GetApplicationMapScale()
        {
            object scale = Application.GetSystemVariable(/*MSG0*/"Userr1");
            if (scale is double)
            {
                return (double) scale;
            }

            return null;
        }

        public static void SetApplicationMapScale(double value)
        {
            Application.SetSystemVariable(/*MSG0*/"Userr1", value);
        }
    }
}
