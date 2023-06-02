using Newtonsoft.Json;

namespace VSRAD.Package.DebugVisualizer
{
    public class Breakpoint : System.IEquatable<Breakpoint>
    {
        public string File { get; }

        public uint Line { get; }

        public bool Resumable { get; set; }

        [JsonConstructor]
        public Breakpoint(string file, uint line, bool resumable)
        {
            File = file;
            Line = line;
            Resumable = resumable;
        }

        public bool Equals(Breakpoint br) => File == br.File && Line == br.Line && Resumable == br.Resumable;
        public override bool Equals(object o) => o is Breakpoint br && Equals(br);
        public override int GetHashCode() => (File, Line, Resumable).GetHashCode();
        public static bool operator ==(Breakpoint left, Breakpoint right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }
        public static bool operator !=(Breakpoint left, Breakpoint right) => !(left == right);
    }
}
