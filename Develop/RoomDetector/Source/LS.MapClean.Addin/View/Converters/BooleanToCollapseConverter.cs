using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LS.MapClean.Addin.View.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToCollapseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = (bool)value;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = (Visibility)value;
            return visibility != Visibility.Collapsed && visibility != Visibility.Hidden;
        }
    }
}
