using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoScheduler.UI.Converters
{
    // MultiBinding helper: returns true if any input is true.
    public sealed class BoolOrConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is bool b && b) return true;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
