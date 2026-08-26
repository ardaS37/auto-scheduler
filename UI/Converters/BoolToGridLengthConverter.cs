using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoScheduler.UI.Converters
{
    public sealed class BoolToGridLengthConverter : IValueConverter
    {
        public GridLength TrueValue { get; set; } = new GridLength(190);
        public GridLength FalseValue { get; set; } = new GridLength(0);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
