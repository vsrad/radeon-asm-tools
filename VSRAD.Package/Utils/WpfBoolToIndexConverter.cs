using System;
using System.Globalization;
using System.Windows.Data;

namespace VSRAD.Package.Utils
{
    public sealed class WpfBoolToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? 0 : 1;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => (int)value == 0;
    }
}
