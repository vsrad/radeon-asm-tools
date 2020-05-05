using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnHighlightRegion : DefaultNotifyPropertyChanged
    {
        private string _selector;
        public string Selector
        {
            get => _selector ?? "";
            set => SetField(ref _selector, value);
        }

        private DataHighlightColor _color;
        [JsonConverter(typeof(BackwardsCompatibilityColorConverter))]
        public DataHighlightColor Color
        {
            get => _color;
            set => SetField(ref _color, value);
        }

        public sealed class BackwardsCompatibilityColorConverter : StringEnumConverter
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String && reader.Value?.ToString() is string color)
                {
                    switch (color)
                    {
                        case "Red": return DataHighlightColor.ColumnRed;
                        case "Green": return DataHighlightColor.ColumnGreen;
                        case "Blue": return DataHighlightColor.ColumnBlue;
                    }
                }
                return base.ReadJson(reader, objectType, existingValue, serializer);
            }
        }
    }
}
