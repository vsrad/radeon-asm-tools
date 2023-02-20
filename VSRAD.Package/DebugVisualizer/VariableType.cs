using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRAD.Package.DebugVisualizer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum VariableRepresentation
    {
#pragma warning disable CA1720 // Identifier contains type name
        Hex, Float, Uint, Int, Bin
#pragma warning restore CA1720 // Identifier contains type name
    };

    public readonly struct VariableType : System.IEquatable<VariableType>
    {
        [JsonConstructor]
        public VariableType(VariableRepresentation type, int size)
        {
            Repr = type;
            Size = size;
        }

        public readonly VariableRepresentation Repr;
        public readonly int Size;

        public bool Equals(VariableType other) =>
            other.Repr == Repr && other.Size == Size;

        public override bool Equals(object obj) =>
            obj is VariableType other && Equals(other);

        public override int GetHashCode() => (Repr, Size).GetHashCode();

        public static bool operator ==(VariableType left, VariableType right) => left.Equals(right);

        public static bool operator !=(VariableType left, VariableType right) => !(left == right);
    }

    public static class VariableTypeUtils
    {
        public static string ShortName(this VariableType info)
        {
            switch (info.Repr)
            {
                case VariableRepresentation.Bin:
                    return "B";
                case VariableRepresentation.Float:
                    if (info.Size == 32)
                        return "F";
                    else
                        return "h"; // half
                case VariableRepresentation.Hex:
                    return "H";
                case VariableRepresentation.Int:
                    return "I" + info.Size.ToString();
                case VariableRepresentation.Uint:
                    return "U" + info.Size.ToString();
                default:
                    return string.Empty;
            }
        }

        public static VariableType TypeFromShortName(string shortName)
        {
            switch (shortName[0])
            {
                case 'B':
                    return new VariableType(VariableRepresentation.Bin, 32);
                case 'F':
                    return new VariableType(VariableRepresentation.Float, 32);
                case 'h':
                    return new VariableType(VariableRepresentation.Float, 16);
                case 'H':
                    return new VariableType(VariableRepresentation.Hex, 32);
                case 'I':
                    return new VariableType(VariableRepresentation.Int, int.Parse(shortName.Substring(1)));
                default:
                    return new VariableType(VariableRepresentation.Uint, int.Parse(shortName.Substring(1)));
            }
        }
    }
}
