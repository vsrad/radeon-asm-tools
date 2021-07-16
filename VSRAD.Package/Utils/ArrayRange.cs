using System.Text.RegularExpressions;

namespace VSRAD.Package.Utils
{
    static class ArrayRange
    {
        private static readonly Regex _numericIndexPattern = new Regex(@"\[\d+\]$", RegexOptions.Compiled);
        private static readonly Regex _symbolIndexPattern = new Regex(@"\[\D+\]$", RegexOptions.Compiled);

        // \[\d+\]$: add new brackets
        //data_N0HW_base_addr[1][2]
        //data_N0HW_base_addr[1]

        // \[\D+\]$: add +index to last brackets
        //data_N0HW_base_addr[1][ii]
        //data_N0HW_base_addr[i]

        // else add new brackets
        public static string[] FormatArrayRangeWatch(string name, int from, int to, bool matchBrackets)
        {
            var numericMatch = _numericIndexPattern.Match(name);
            var symbolMatch = _symbolIndexPattern.Match(name);

            var count = to - from + 1;
            var result = new string[count];

            if ((numericMatch.Success || (!numericMatch.Success && !symbolMatch.Success)) || !matchBrackets)
            {
                for (int i = 0; i < count; i++)
                    result[i] = $"{name}[{from + i}]";
            }
            else
            {
                for (int i = 0; i < count; i++)
                    result[i] = from + i < 0
                        ? name.Replace(symbolMatch.Value, $"{symbolMatch.Value.TrimEnd(']')}{from + i}]")
                        : name.Replace(symbolMatch.Value, $"{symbolMatch.Value.TrimEnd(']')}+{from + i}]");
            }
            return result;
        }
    }
}
