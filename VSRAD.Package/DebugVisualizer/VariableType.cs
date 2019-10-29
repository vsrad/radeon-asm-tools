namespace VSRAD.Package.DebugVisualizer
{
    public enum VariableType
    {
        Hex, Float, Uint, Int, Half, Bin
    };

    public static class VariableTypeUtils
    {
        public static string ShortName(this VariableType type)
        {
            switch (type)
            {
                case VariableType.Bin:
                    return "B";
                case VariableType.Float:
                    return "F";
                case VariableType.Half:
                    return "h";
                case VariableType.Hex:
                    return "H";
                case VariableType.Int:
                    return "I";
                case VariableType.Uint:
                    return "U";
                default:
                    return string.Empty;
            }
        }

        public static VariableType TypeFromShortName(string shortName)
        {
            switch (shortName)
            {
                case "B":
                    return VariableType.Bin;
                case "F":
                    return VariableType.Float;
                case "h":
                    return VariableType.Half;
                case "H":
                    return VariableType.Hex;
                case "I":
                    return VariableType.Int;
                default:
                    return VariableType.Uint;
            }
        }
    }
}
