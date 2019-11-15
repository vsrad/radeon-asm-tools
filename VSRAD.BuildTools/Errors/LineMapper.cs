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

        public static string HostSourcePathMapping(string remotePath, string[] projectPaths)
        {
            var remotePathArray = remotePath.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
            Array.Reverse(remotePathArray);
            string probablePath = "";
            int longestMatch = -1;
            foreach (var path in projectPaths)
            {
                var pathArray = path.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
                Array.Reverse(pathArray);
                int matchCount = 0;

                while (matchCount < pathArray.Length && matchCount < remotePathArray.Length && remotePathArray[matchCount] == pathArray[matchCount])
                    matchCount++;

                if (matchCount > longestMatch)
                {
                    probablePath = path;
                    longestMatch = matchCount;
                }
            }
            return probablePath;
        }
    }
}
