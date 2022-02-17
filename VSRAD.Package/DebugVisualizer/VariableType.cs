namespace VSRAD.Package.DebugVisualizer
{
    public enum VariableType
    {
#pragma warning disable CA1720 // Identifier contains type name
        Hex, Float, Uint, Int, Half, Bin
#pragma warning restore CA1720 // Identifier contains type name
    };

    public struct VariableInfo : System.IEquatable<VariableInfo>
    {
        public VariableType Type;
        public int Size;

        public bool Equals(VariableInfo other) =>
            other.Type == Type && other.Size == Size;

        public override bool Equals(object obj) =>
            obj is VariableInfo other && Equals(other);

        public override int GetHashCode() => (Type, Size).GetHashCode();

        public static bool operator ==(VariableInfo left, VariableInfo right) => left.Equals(right);

        public static bool operator !=(VariableInfo left, VariableInfo right) => !(left == right);
    }

    public static class VariableTypeUtils
    {
        public static string ShortName(this VariableInfo info)
        {
            switch (info.Type)
            {
                case VariableType.Bin:
                    return "B" + info.Size.ToString();
                case VariableType.Float:
                    return "F" + info.Size.ToString();
                case VariableType.Half:
                    return "h"; // we dont use size for half's
                case VariableType.Hex:
                    return "H" + info.Size.ToString();
                case VariableType.Int:
                    return "I" + info.Size.ToString();
                case VariableType.Uint:
                    return "U" + info.Size.ToString();
                default:
                    return string.Empty;
            }
        }

        public static VariableInfo TypeFromShortName(string shortName)
        {
            switch (shortName[0])
            {
                case 'B':
                    return new VariableInfo { Type = VariableType.Bin,   Size = int.Parse(shortName.Substring(1)) };
                case 'F':
                    return new VariableInfo { Type = VariableType.Float, Size = int.Parse(shortName.Substring(1)) };
                case 'h':
                    return new VariableInfo { Type = VariableType.Half,  Size = 0 /*we dont use size for half's*/ };
                case 'H':
                    return new VariableInfo { Type = VariableType.Hex,   Size = int.Parse(shortName.Substring(1)) };
                case 'I':
                    return new VariableInfo { Type = VariableType.Int,   Size = int.Parse(shortName.Substring(1)) };
                default:
                    return new VariableInfo { Type = VariableType.Uint,  Size = int.Parse(shortName.Substring(1)) };
            }
        }
    }
}
