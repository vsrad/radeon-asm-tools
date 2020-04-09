using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Drawing;

namespace VSRAD.Package.Utils
{
    public static class SeparatorColorConverter
    {
        public static SolidBrush ConvertToBrush(string value)
        {
            var hexString = value.ToString();
            if (hexString.Length != 6) return new SolidBrush(Color.Black);
            if (!int.TryParse(hexString.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int red))
                return new SolidBrush(Color.Black);
            if (!int.TryParse(hexString.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int green))
                return new SolidBrush(Color.Black);
            if (!int.TryParse(hexString.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int blue))
                return new SolidBrush(Color.Black);
            return new SolidBrush(Color.FromArgb(red, green, blue));
        }
    }
}
