using System;
using System.Text.RegularExpressions;

namespace VSRAD.BuildTools.Errors
{
    public static class LineMapper
    {
        private static readonly Regex LineNumRegex = new Regex(@"\d+", RegexOptions.Compiled);

        public static int[] MapLines(string preprocessed)
        {
            var lines = preprocessed.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int[] result = new int[lines.Length];
            int originalSourceLine = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("#") || line.StartsWith("//#"))
                {
                    originalSourceLine = int.Parse(LineNumRegex.Match(line).Value);
                    continue;
                }
                result[i] = originalSourceLine++;
            }
            return result;
        }
    }
}
