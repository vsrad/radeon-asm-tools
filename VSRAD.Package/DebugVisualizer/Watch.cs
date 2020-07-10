using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRAD.Package.DebugVisualizer
{
    public readonly struct Watch : System.IEquatable<Watch>
    {
        public string Name { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public VariableType Type { get; }

        public bool IsAVGPR { get; }

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name);

        [JsonConstructor]
        public Watch(string name, VariableType type, bool isAVGPR)
        {
            Name = name;
            Type = type;
            IsAVGPR = isAVGPR;
        }

        public bool Equals(Watch w) => Name == w.Name && Type == w.Type && IsAVGPR == w.IsAVGPR;
        public override bool Equals(object o) => o is Watch w && Equals(w);
        public override int GetHashCode() => (Name, Type, IsAVGPR).GetHashCode();
        public static bool operator ==(Watch left, Watch right) => left.Equals(right);
        public static bool operator !=(Watch left, Watch right) => !(left == right);
    }
}
