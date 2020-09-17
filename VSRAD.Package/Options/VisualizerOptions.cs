using Microsoft.VisualStudio.Utilities;
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

        private bool _verticalSplit = true;
        public bool VerticalSplit { get => _verticalSplit; set => SetField(ref _verticalSplit, value); }

        private uint _laneGrouping = 0;
        public uint LaneGrouping { get =>  _laneGrouping; set => SetField(ref _laneGrouping, value); }

        private uint _waveSize = 64;
        public uint WaveSize { get => _waveSize; set => SetField(ref _waveSize, value); }

        private bool _checkMagicNumber = true;
        public bool CheckMagicNumber { get => _checkMagicNumber; set => SetField(ref _checkMagicNumber, value); }

        private int _magicNumber = 0x7777777; // Default value, do not change
        [JsonConverter(typeof(MagicNumberConverter))]
        public int MagicNumber { get => _magicNumber; set => SetField(ref _magicNumber, value); }

        private bool _showColumnsField;
        [DefaultValue(true)]
        public bool ShowColumnsField { get => _showColumnsField; set => SetField(ref _showColumnsField, value); }

        private bool _showAppArgsField;
        [DefaultValue(true)]
        public bool ShowAppArgsField { get => _showAppArgsField; set => SetField(ref _showAppArgsField, value); }

        private bool _showBreakArgsField;
        [DefaultValue(true)]
        public bool ShowBreakArgsField { get => _showBreakArgsField; set => SetField(ref _showBreakArgsField, value); }
    }

    public sealed class MagicNumberConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                if (((string)reader.Value).StartsWith("0x", StringComparison.Ordinal))
                    return int.Parse(((string)reader.Value).Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                    return int.Parse((string)reader.Value);
            else
                return existingValue;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(int);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var hexValue = $"0x{((int)value).ToString("x")}";
            var token = JToken.FromObject(hexValue);
            token.WriteTo(writer);
        }
    }
}
