using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LS.MapClean.Addin.Settings;

namespace LS.MapClean.Addin.View.Converters
{
    public class ErrorMarkShapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var actionType = (MapClean.ActionType) value;
            var markshape = ErrorMarkSettings.CurrentSettings.MarkShapes[actionType];
            var pathGeometry = ErrorMarkShapeGenerator.Instance.GetShape(markshape);
            return pathGeometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorMarkShapeGenerator
    {
        #region Singleton
        private static volatile ErrorMarkShapeGenerator _instance;
        static object _syncRoot = new object();
        public static ErrorMarkShapeGenerator Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        // Double check.
                        if (_instance == null)
                        {
                            _instance = new ErrorMarkShapeGenerator();
                        }
                    }
                }

                return _instance;
            }
        }
        #endregion

        #region Initialization
        protected ErrorMarkShapeGenerator()
        {
            InitializeShapes();
        }

        private void InitializeShapes()
        {
            var resource = new ResourceDictionary();
            resource.Source = new Uri("pack://application:,,,/LS.MapClean.Addin;component/View/ErrorMarkShapes.xaml");
            var shapes = Enum.GetValues(typeof (MarkShape));
            foreach (MarkShape shape in shapes)
            {
                var key = shape.ToString() + "Shape";
                var pathGeometry = (PathGeometry)resource[key];
                _shapes.Add(shape, pathGeometry);
            }
        }
        #endregion

        #region Shapes
        private Dictionary<MarkShape, PathGeometry> _shapes = new Dictionary<MarkShape, PathGeometry>();
        public PathGeometry GetShape(MarkShape shapeType)
        {
            return _shapes[shapeType];
        }
        #endregion
    }
}
