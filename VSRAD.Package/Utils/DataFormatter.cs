using System;
using System.Text;
using VSRAD.Package.DebugVisualizer;

namespace VSRAD.Package.Utils
{
    static class DataFormatter
    {
        private static string InsertNumberSeparators(string str, uint sep)
        {
            var sb = new StringBuilder();
            // If leading zeroes option is turned off we want separators
            // to be between N characters starting from last one, not the
            // first: 0x3 13, not 0x31 3, considering we have separators
            // between every two numbers. That's why string is actually
            // processed from the last character to first
            for (int i = str.Length - 1, s = (int)sep - 1; i >= 0; i--, s--)
            {
                sb.Insert(0, str[i]);
                if (s == 0 && i != 0) // exclude i == 0 to avoid extra space after type prefix
                {
                    sb.Insert(0, " ");
                    s = (int)sep;
                }
            }
            return sb.ToString();
        }

        public static string FormatDword(VariableInfo varInfo, uint data, uint binHexSeparator, uint intSeparator, bool leadingZeroes)
        {
            switch (varInfo.Type)
            {
                case VariableType.Hex:
                    var hex = data.ToString("x");
                    if (leadingZeroes)
                        hex = hex.PadLeft(8, '0');
                    if (binHexSeparator != 0)
                        hex = InsertNumberSeparators(hex, binHexSeparator);
                    return "0x" + hex;
                case VariableType.Float:
                    return BitConverter.ToSingle(BitConverter.GetBytes(data), 0).ToString();
                case VariableType.Uint:
                    return intSeparator == 0 ? data.ToString() : InsertNumberSeparators(data.ToString(), intSeparator);
                case VariableType.Int:
                    return intSeparator == 0 ? ((int)data).ToString() : InsertNumberSeparators(((int)data).ToString(), intSeparator);
                case VariableType.Half:
                    byte[] bytes = BitConverter.GetBytes(data);
                    float firstHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 0));
                    float secondHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 2));
                    return $"({firstHalf}; {secondHalf})";
                case VariableType.Bin:
                    var bin = Convert.ToString(data, 2);
                    if (leadingZeroes)
                        bin = bin.PadLeft(32, '0');
                    if (binHexSeparator != 0)
                        bin = InsertNumberSeparators(bin, binHexSeparator);
                    return "0b" + bin;
                default:
                    return string.Empty;
            }
        }
    }
}
