using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class VisualizerOptions : DefaultNotifyPropertyChanged
    {
        private bool _showSystemVariable = true;
        public bool ShowSystemVariable
        {
            get => _showSystemVariable;
            set => SetField(ref _showSystemVariable, value);
        }

        private bool _maskLanes = true;
        public bool MaskLanes
        {
            get => _maskLanes;
            set => SetField(ref _maskLanes, value);
        }

        private bool _ndrange3d = false;
        public bool NDRange3D
        {
            get => _ndrange3d;
            set => SetField(ref _ndrange3d, value);
        }

        private uint _laneGrouping = 0;
        public uint LaneGrouping
        {
            get => _laneGrouping;
            set => SetField(ref _laneGrouping, value);
        }

        private bool _checkMagicNumber = true;
        public bool CheckMagicNumber
        {
            get => _checkMagicNumber;
            set => SetField(ref _checkMagicNumber, value);
        }

        private int _magicNumber = 2004318071; // Default value, do not change
        [JsonConverter(typeof(MagicNumberConverter))]
        public int MagicNumber
        {
            get => _magicNumber;
            set => SetField(ref _magicNumber, value);
        }

        private bool _showAppArgs = false;
        public bool ShowAppArgs
        {
            get => _showAppArgs;
            set => SetField(ref _showAppArgs, value);
        }

        private bool _showBrkArgs = false;
        public bool ShowBrkArgs
        {
            get => _showBrkArgs;
            set => SetField(ref _showBrkArgs, value);
        }
    }

    public sealed class MagicNumberConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                if (((string)reader.Value).StartsWith("0x"))
                    return int.Parse(((string)reader.Value).Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
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
