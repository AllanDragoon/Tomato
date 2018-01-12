using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LS.MapClean.Addin.View.Converters
{
    public class TreeItemIndentConverter : IValueConverter
    {
        const double _indentPerLevel = 12; // in pixel
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Get its level
            var level = 0;
            if (value is TreeViewItem)
            {
                var parent = VisualTreeHelper.GetParent(value as DependencyObject);
                while (!(parent is TreeView) && (parent != null))
                {
                    if (parent is TreeViewItem)
                        level++;
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            return new Thickness(level * _indentPerLevel, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(/*MSG0*/"The 'ConvertBack' is not implemented.");
        }
    }
}
