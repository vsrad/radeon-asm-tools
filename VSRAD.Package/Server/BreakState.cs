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
        public uint WaveSize { get; }
        public uint DimX { get; }
        public uint DimY { get; }
        public uint DimZ { get; }
        public bool NDRange3D { get; }

        private static readonly Regex StatusFileRegex = new Regex(@"grid size \((?<gd_x>\d+), (?<gd_y>\d+), (?<gd_z>\d+)\)\s+group size \((?<gp_x>\d+), (?<gp_y>\d+), (?<gp_z>\d+)\)\s+wave size (?<wv>\d+)\s+comment (?<comment>.+)", RegexOptions.Compiled);

        public BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusFileContents)
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = 0;
            ExitCode = 0;

            var match = StatusFileRegex.Match(statusFileContents);
            if (match.Success)
            {
                var gridX = uint.Parse(match.Groups["gd_x"].Value);
                var gridY = uint.Parse(match.Groups["gd_y"].Value);
                var gridZ = uint.Parse(match.Groups["gd_z"].Value);
                var groupX = uint.Parse(match.Groups["gp_x"].Value);
                var groupY = uint.Parse(match.Groups["gp_y"].Value);
                var groupZ = uint.Parse(match.Groups["gp_z"].Value);

                DimX = gridX / groupX;
                DimY = gridY / groupY;
                DimZ = gridZ / groupZ;
                NDRange3D = gridY != 0 && gridZ != 0;
                WaveSize = uint.Parse(match.Groups["wv"].Value);
                StatusString = match.Groups["comment"].Value;
            }
        }
    }
}
