using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class VisualizerOptions : DefaultNotifyPropertyChanged
    {
        private bool _showSystemVariable = true;
        public bool ShowSystemVariable { get => _showSystemVariable; set => SetField(ref _showSystemVariable, value); }

        private bool _maskLanes = true;
        public bool MaskLanes { get => _maskLanes; set => SetField(ref _maskLanes, value); }

        private bool _ndrange3d = false;
        public bool NDRange3D { get => _ndrange3d; set => SetField(ref _ndrange3d, value); }

        private int _wavemapElementSize = 8;
        [DefaultValue(8)]
        public int WavemapElementSize { get => _wavemapElementSize; set => SetField(ref _wavemapElementSize, Math.Max(value, 7)); }

        private bool _checkMagicNumber = true;
        public bool CheckMagicNumber { get => _checkMagicNumber; set => SetField(ref _checkMagicNumber, value); }

        private uint _magicNumber = 0x7777777; // Default value, do not change
        [JsonConverter(typeof(MagicNumberConverter))]
        public uint MagicNumber { get => _magicNumber; set => SetField(ref _magicNumber, value); }

        private bool _manualMode;
        [DefaultValue(false)]
        public bool ManualMode { get => _manualMode; set => SetField(ref _manualMode, value); }

        private bool _showColumnsField;
        [DefaultValue(true)]
        public bool ShowColumnsField { get => _showColumnsField; set => SetField(ref _showColumnsField, value); }

        private bool _showAppArgsField;
        [DefaultValue(true)]
        public bool ShowAppArgsField { get => _showAppArgsField; set => SetField(ref _showAppArgsField, value); }

        private bool _showAppArgs2Field;
        [DefaultValue(true)]
        public bool ShowAppArgs2Field { get => _showAppArgs2Field; set => SetField(ref _showAppArgs2Field, value); }

        private bool _showAppArgs3Field;
        [DefaultValue(true)]
        public bool ShowAppArgs3Field { get => _showAppArgs3Field; set => SetField(ref _showAppArgs3Field, value); }

        private bool _showWavemap;
        [DefaultValue(true)]
        public bool ShowWavemap { get => _showWavemap; set => SetField(ref _showWavemap, value); }

        private bool _matchBracketsOnAddToWatches;
        [DefaultValue(false)]
        public bool MatchBracketsOnAddToWatches { get => _matchBracketsOnAddToWatches; set => SetField(ref _matchBracketsOnAddToWatches, value); }
    }

    public sealed class MagicNumberConverter : JsonConverter
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
            return existingValue;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(int);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var hexValue = $"0x{(uint)value:x}";
            var token = JToken.FromObject(hexValue);
            token.WriteTo(writer);
        }
    }
}
