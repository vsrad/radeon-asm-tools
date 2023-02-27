using System;
using System.Text;
using VSRAD.Package.DebugVisualizer;

namespace VSRAD.Package.Utils
{
    static class DataFormatter
    {
        private static string InsertNumberSeparators(string str, uint sep)
        {
            if (sep == 0) return str;
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

        public static string FormatDword(VariableType varInfo, uint data, uint binHexSeparator, uint intSeparator, bool leadingZeroes)
        {
            switch (varInfo.Category)
            {
                case VariableCategory.Hex:
                    var hex = data.ToString("x");
                    if (varInfo.Size != 32) hex = hex.Substring(8 - (varInfo.Size / 4), varInfo.Size / 4); // TODO: doesnt work with short representations (0x0)
                    if (string.IsNullOrEmpty(hex)) hex = "0";
                    if (leadingZeroes)
                        hex = hex.PadLeft(varInfo.Size / 4, '0');
                    if (binHexSeparator != 0)
                        hex = InsertNumberSeparators(hex, binHexSeparator);
                    return "0x" + hex;
                case VariableCategory.Float:
                    switch (varInfo.Size)
                    {
                        case 16:
                            byte[] bytes = BitConverter.GetBytes(data);
                            float firstHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 0));
                            float secondHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 2));
                            return $"{firstHalf}; {secondHalf}";
                        case 32:
                            return BitConverter.ToSingle(BitConverter.GetBytes(data), 0).ToString();
                        default:
                            throw new NotImplementedException($"Unknown size: {varInfo.Size}");
                    }
                case VariableCategory.Uint:
                    var uIntBytes = BitConverter.GetBytes(data);
                    switch (varInfo.Size)
                    {
                        case 32:
                            return InsertNumberSeparators(data.ToString(), intSeparator);
                        case 16:
                            var res16_1 = InsertNumberSeparators(BitConverter.ToUInt16(uIntBytes, 2).ToString(), intSeparator);
                            var res16_2 = InsertNumberSeparators(BitConverter.ToUInt16(uIntBytes, 0).ToString(), intSeparator);
                            return $"{res16_1}; {res16_2}";
                        case 8:
                            var res8_1 = InsertNumberSeparators(uIntBytes[3].ToString(), intSeparator);
                            var res8_2 = InsertNumberSeparators(uIntBytes[2].ToString(), intSeparator);
                            var res8_3 = InsertNumberSeparators(uIntBytes[1].ToString(), intSeparator);
                            var res8_4 = InsertNumberSeparators(uIntBytes[0].ToString(), intSeparator);
                            return $"{res8_1}; {res8_2}; {res8_3}; {res8_4}";
                        default:
                            throw new NotImplementedException($"Unknown size: {varInfo.Size}");
                    }
                case VariableCategory.Int:
                    var intBytes = BitConverter.GetBytes(data);
                    switch (varInfo.Size)
                    {
                        case 32:
                            return InsertNumberSeparators(((int)data).ToString(), intSeparator);
                        case 16:
                            var res16_1 = InsertNumberSeparators(BitConverter.ToInt16(intBytes, 2).ToString(), intSeparator);
                            var res16_2 = InsertNumberSeparators(BitConverter.ToInt16(intBytes, 0).ToString(), intSeparator);
                            return $"{res16_1}; {res16_2}";
                        case 8:
                            var res8_1 = InsertNumberSeparators(((sbyte)intBytes[3]).ToString(), intSeparator);
                            var res8_2 = InsertNumberSeparators(((sbyte)intBytes[2]).ToString(), intSeparator);
                            var res8_3 = InsertNumberSeparators(((sbyte)intBytes[1]).ToString(), intSeparator);
                            var res8_4 = InsertNumberSeparators(((sbyte)intBytes[0]).ToString(), intSeparator);
                            return $"{res8_1}; {res8_2}; {res8_3}; {res8_4}";
                        default:
                            throw new NotImplementedException($"Unknown size: {varInfo.Size}");
                    }
                case VariableCategory.Bin:
                    var bin = Convert.ToString(data, 2).PadLeft(32, '0');
                    if (varInfo.Size != 32) bin = bin.Substring(32 - varInfo.Size, varInfo.Size);
                    if (string.IsNullOrEmpty(bin)) bin = "0";
                    if (!leadingZeroes)
                        bin = bin.TrimStart('0');
                    if (binHexSeparator != 0)
                        bin = InsertNumberSeparators(bin, binHexSeparator);
                    return "0b" + bin;
                default:
                    return string.Empty;
            }
        }
    }
}
