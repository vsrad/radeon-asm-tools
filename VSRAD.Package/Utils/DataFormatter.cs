using System;
using VSRAD.Package.DebugVisualizer;

namespace VSRAD.Package.Utils
{
    static class DataFormatter
    {
        public static string FormatDword(VariableType variableType, uint data)
        {
            switch (variableType)
            {
                case VariableType.Hex:
                    return "0x" + data.ToString("x");
                case VariableType.Float:
                    return BitConverter.ToSingle(BitConverter.GetBytes(data), 0).ToString();
                case VariableType.Uint:
                    return data.ToString();
                case VariableType.Int:
                    return ((int)data).ToString();
                case VariableType.Half:
                    byte[] bytes = BitConverter.GetBytes(data);
                    float firstHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 0));
                    float secondHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 2));
                    return $"({firstHalf}; {secondHalf})";
                case VariableType.Bin:
                    return "0b" + Convert.ToString(data, 2);
                default:
                    return string.Empty;
            }
        }
    }
}
