using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace LS.MapClean.Addin.View.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class FlipBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return InverseBoolean(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return InverseBoolean(value);
        }

        static object InverseBoolean(object value)
        {
            if (value == null)
                return Binding.DoNothing;

            Boolean input = false;

            // try parse the value since it might not a boolean
            // force cast may cause crash!
            bool result = Boolean.TryParse(value.ToString(), out input);

            // return visible if the input is wrong
            if (!result)
                return true;

            // inverse the input boolean
            return !input;
        }
    }
}
