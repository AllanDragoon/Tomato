using Autodesk.AutoCAD.Colors;

namespace LS.MapClean.Addin.SpreadPoints
{
    class SpreadPointSettings
    {
        public SpreadPointSettings()
        {
            // Compatible with South CASS.
            LayerName = "ZDH";
            LayerColor = SerializableColor.FromAcadColor(Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 1));
            // BYLAYER
            Color = SerializableColor.FromAcadColor(Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByLayer, 256));
        
            // Annotation x-offset and y-offset.
            AnnotationXOffset = 0.2500;
            AnnotationYOffset = -0.3750;
            Scale = 1.0;

            // Font height
            FontHeight = 1.000;

            // Text style name
            TextStyleName = "HZ";

            // Text style font file name
            TextStyleFontFileName = "rs.shx";
            TextStyleBigFontFileName = "hztxt.shx";
        }

        public bool InsertId { get; set; }
        public bool InsertCode { get; set; }

        public string LayerName { get; set; }
        public SerializableColor LayerColor { get; set; }
        public SerializableColor Color { get; set; }

        public double AnnotationXOffset { get; set; }
        public double AnnotationYOffset { get; set; }

        public double Scale { get; set; }

        public string TextStyleName { get; set; }
        public double FontHeight { get; set; }
        public string TextStyleFontFileName { get; set; }
        public string TextStyleBigFontFileName { get; set; }
    }

    class SerializableColor
    {
        /// <summary>
        /// Color method
        /// </summary>
        public ColorMethod ColorMethod { get; set; }

        /// <summary>
        /// Color Index
        /// </summary>
        public short ColorIndex { get; set; }

        /// <summary>
        /// Color's RGB
        /// </summary>
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }

        public SerializableColor()
        {
            InitSerializableColor();
        }

        private void InitSerializableColor()
        {
            var acadColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 1);
            ColorMethod = acadColor.ColorMethod;
            ColorIndex = acadColor.ColorIndex;
            Red = acadColor.ColorValue.R;
            Green = acadColor.ColorValue.G;
            Blue = acadColor.ColorValue.B;
        }

        public Autodesk.AutoCAD.Colors.Color ToAcadColor()
        {
            Autodesk.AutoCAD.Colors.Color result = null;
            switch (ColorMethod)
            {
                case ColorMethod.ByLayer:
                case ColorMethod.ByBlock:
                case ColorMethod.ByAci:
                    result = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod, ColorIndex);
                    break;
                default:
                    result = Autodesk.AutoCAD.Colors.Color.FromRgb(Red, Green, Blue);
                    break;
            }
            return result;
        }

        public System.Windows.Media.Color ToWindowsColor()
        {
            return System.Windows.Media.Color.FromRgb(Red, Green, Blue);
        }

        public static SerializableColor FromAcadColor(Autodesk.AutoCAD.Colors.Color color)
        {
            var result = new SerializableColor()
            {
                ColorMethod = color.ColorMethod,
                ColorIndex = color.ColorIndex,
                Red = color.ColorValue.R,
                Green = color.ColorValue.G,
                Blue = color.ColorValue.B
            };
            return result;
        }
    }
}
