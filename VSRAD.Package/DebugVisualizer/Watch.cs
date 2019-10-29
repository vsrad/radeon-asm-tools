using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRAD.Package.DebugVisualizer
{
    public readonly struct Watch
    {
        public string Name { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public VariableType Type { get; }

        public bool IsAVGPR { get; }

        [JsonConstructor]
        public Watch(string name, VariableType type, bool isAVGPR)
        {
            Name = name;
            Type = type;
            IsAVGPR = isAVGPR;
        }
    }
}
