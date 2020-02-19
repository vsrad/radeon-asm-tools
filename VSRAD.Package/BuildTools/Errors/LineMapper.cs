using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VSRAD.Package.BuildTools.Errors
{
    public struct LineMarker : IEquatable<LineMarker>
    {
        public int PpLine { get; }
        public int SourceLine { get; }
        public string SourceFile { get; }

        public LineMarker(int ppLine, int sourceLine, string sourceFile)
        {
            PpLine = ppLine;
            SourceLine = sourceLine;
            SourceFile = sourceFile;
        }

        public bool Equals(LineMarker m) => PpLine == m.PpLine && SourceLine == m.SourceLine && SourceFile == m.SourceFile;
        public override bool Equals(object o) => o is LineMarker m && Equals(m);
        public override int GetHashCode() => (PpLine, SourceLine, SourceFile).GetHashCode();
        public static bool operator ==(LineMarker left, LineMarker right) => left.Equals(right);
        public static bool operator !=(LineMarker left, LineMarker right) => !(left == right);
    }

    public static class LineMapper
    {
        private static readonly Regex LineMarkerRegex = new Regex(@"^\s*(//)?#\s+(?<line>\d+)\s+\""(?<file>.*)\""", RegexOptions.Compiled);

        public static List<LineMarker> MapLines(string preprocessed)
        {
            var lines = preprocessed.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<LineMarker>();
            for (int i = 1; i <= lines.Length; i++)
            {
                var line = lines[i - 1];
                var match = LineMarkerRegex.Match(line);

                if (match.Success)
                    result.Add(new LineMarker(
                        //  +1 for next line after marker
                        ppLine: i + 1,
                        sourceLine: int.Parse(match.Groups["line"].Value),
                        sourceFile: match.Groups["file"].Value));
            }
            return result;
        }

        public static string MapSourceToHost(string remotePath, IEnumerable<string> projectPaths)
        {
            var remotePathArray = remotePath.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
            Array.Reverse(remotePathArray);
            string probablePath = remotePath;
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
