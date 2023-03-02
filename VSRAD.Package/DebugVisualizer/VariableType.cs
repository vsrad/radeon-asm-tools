using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRAD.Package.DebugVisualizer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum VariableCategory
    {
#pragma warning disable CA1720 // Identifier contains type name
        Hex, Float, Uint, Int, Bin
#pragma warning restore CA1720 // Identifier contains type name
    };

    public readonly struct VariableType : System.IEquatable<VariableType>
    {
        [JsonConstructor]
        public VariableType(VariableCategory category, int size)
        {
            Category = category;
            Size = size;
        }

        public readonly VariableCategory Category;
        public readonly int Size;

        public bool Equals(VariableType other) =>
            other.Category == Category && other.Size == Size;

        public override bool Equals(object obj) =>
            obj is VariableType other && Equals(other);

        public override int GetHashCode() => (Category, Size).GetHashCode();

        public static bool operator ==(VariableType left, VariableType right) => left.Equals(right);

        public static bool operator !=(VariableType left, VariableType right) => !(left == right);
    }

    public static class VariableTypeUtils
    {
        public static string ShortName(this VariableType info)
        {
            switch (info.Category)
            {
                case VariableCategory.Bin:
                    return "B";
                case VariableCategory.Float:
                    return "F" + info.Size.ToString();
                case VariableCategory.Hex:
                    return "H";
                case VariableCategory.Int:
                    return "I" + info.Size.ToString();
                case VariableCategory.Uint:
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
                    return new VariableType(VariableCategory.Bin, 32);
                case 'F':
                    return new VariableType(VariableCategory.Float, int.Parse(shortName.Substring(1)));
                case 'H':
                    return new VariableType(VariableCategory.Hex, 32);
                case 'I':
                    return new VariableType(VariableCategory.Int, int.Parse(shortName.Substring(1)));
                default:
                    return new VariableType(VariableCategory.Uint, int.Parse(shortName.Substring(1)));
            }
        }
    }
}
