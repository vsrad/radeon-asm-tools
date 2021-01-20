using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSRAD.Package.Utils
{
    public sealed class MagicNumberConverter : IValueConverter
    {
        private bool _enteredLeadingZero = false;
        private bool _enteredDecimal = false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var magicNumber = value.ToString();
            if (magicNumber.StartsWith("0x", StringComparison.Ordinal) && uint.TryParse(
                magicNumber.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint converted))
            {
                _enteredDecimal = false;
                _enteredLeadingZero = false;
                return converted;
            }
            if (uint.TryParse(magicNumber, out converted))
            {
                _enteredLeadingZero = magicNumber.StartsWith("0", StringComparison.Ordinal);
                _enteredDecimal = true;
                return converted;
            }
            return DependencyProperty.UnsetValue;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is uint magicNumber)
            {
                if (_enteredDecimal)
                    return magicNumber.ToString();
                else if (_enteredLeadingZero)
                    return $"0{magicNumber}";
                else
                    return $"0x{magicNumber:x}";
            }
            return "";
        }
    }
}
