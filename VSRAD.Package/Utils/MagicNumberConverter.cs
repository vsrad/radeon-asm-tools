using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSRAD.Package.Utils
{
    public sealed class JsonMagicNumberConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                if (((string)reader.Value).StartsWith("0x", StringComparison.Ordinal))
                {
                    if (uint.TryParse(((string)reader.Value).Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
                        return parsed;
                }
                else
                {
                    if (uint.TryParse((string)reader.Value, out var parsed))
                        return parsed;
                }
            }
            return null;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(uint?);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var hexValue = value is uint v ? $"0x{v:x}" : null;
            var token = JToken.FromObject(hexValue);
            token.WriteTo(writer);
        }
    }

    public sealed class WpfMagicNumberConverter : IValueConverter
    {
        private bool _enteredLeadingZero = false;
        private bool _enteredDecimal = false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var magicNumber = value.ToString();
            if (string.IsNullOrEmpty(magicNumber))
            {
                return null;
            }
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
