using Newtonsoft.Json;

namespace VSRAD.Package.DebugVisualizer
{
    public readonly struct Watch : System.IEquatable<Watch>
    {
        public string Name { get; }

        public VariableType Info { get; }

        [JsonConstructor]
        public Watch(string name, VariableType type)
        {
            Name = name;
            Info = type;
        }

        public static bool IsWatchNameValid(string name) =>
            !string.IsNullOrWhiteSpace(name) && !name.Contains(ProjectSystem.Macros.RadMacros.WatchSeparator);

        public bool Equals(Watch w) => Name == w.Name && Info == w.Info;
        public override bool Equals(object o) => o is Watch w && Equals(w);
        public override int GetHashCode() => (Name, Info).GetHashCode();
        public static bool operator ==(Watch left, Watch right) => left.Equals(right);
        public static bool operator !=(Watch left, Watch right) => !(left == right);
    }
}
