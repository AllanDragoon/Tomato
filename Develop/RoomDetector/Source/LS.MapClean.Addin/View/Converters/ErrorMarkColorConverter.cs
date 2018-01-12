using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using LS.MapClean.Addin.Settings;

namespace LS.MapClean.Addin.View.Converters
{
    public class ErrorMarkColorConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var actionType = (MapClean.ActionType)value;
            var color = ErrorMarkSettings.CurrentSettings.MarkColors[actionType];
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
