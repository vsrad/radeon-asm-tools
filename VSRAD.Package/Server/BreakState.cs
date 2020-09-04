using System;
using System.Text.RegularExpressions;

namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public BreakStateData Data { get; }
        public long TotalElapsedMilliseconds { get; }
        public long ExecElapsedMilliseconds { get; }
        public string StatusString { get; }
        public int ExitCode { get; }
        public DateTime ExecutedAt { get; } = DateTime.Now;
        public uint GridX;
        public uint GridY;
        public uint GridZ;
        public uint GroupX;
        public uint GroupY;
        public uint GroupZ;
        public uint WaveSize;

        private static readonly Regex StatuFileRegex = new Regex(@"grid size \((?<gd_x>\d+), (?<gd_y>\d+), (?<gd_z>\d+)\)\s+group size \((?<gp_x>\d+), (?<gp_y>\d+), (?<gp_z>\d+)\)\s+wave size (?<wv>\d+)\s+comment (?<comment>.+)", RegexOptions.Compiled);

        public BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusFileContents)
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = 0;
            ExitCode = 0;

            var match = StatuFileRegex.Match(statusFileContents);
            if (match.Success)
            {
                GridX = uint.Parse(match.Groups["gd_x"].Value);
                GridY = uint.Parse(match.Groups["gd_y"].Value);
                GridZ = uint.Parse(match.Groups["gd_z"].Value);
                GroupX = uint.Parse(match.Groups["gp_x"].Value);
                GroupY = uint.Parse(match.Groups["gp_y"].Value);
                GroupZ = uint.Parse(match.Groups["gp_z"].Value);
                WaveSize = uint.Parse(match.Groups["wv"].Value);
                StatusString = match.Groups["comment"].Value;
            }
        }
    }
}
