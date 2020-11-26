using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSRAD.Package.Utils
{
    public sealed class WpfBoolToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? 0 : 1;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => (int)value == 0;
    }

    public sealed class WpfInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    }

    public sealed class WpfBoolToVisibilityConverter : WpfBoolToValueConverter<Visibility> { }

    public sealed class WpfBoolToStringConverter : WpfBoolToValueConverter<string> { }

    public class WpfBoolToValueConverter<T> : IValueConverter
    {
        public T TrueValue { get; set; }

        public T FalseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool v && v) ? TrueValue : FalseValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is T v && EqualityComparer<T>.Default.Equals(v, TrueValue);
    }
}
