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
        public uint GroupSize { get; } // TODO: handle 3d group sizes
        public bool NDRange3D { get; }

        private static readonly Regex StatusFileRegex = new Regex(@"grid size \((?<gd_x>\d+), (?<gd_y>\d+), (?<gd_z>\d+)\)\s+group size \((?<gp_x>\d+), (?<gp_y>\d+), (?<gp_z>\d+)\)\s+wave size (?<wv>\d+)\s+comment (?<comment>.+)", RegexOptions.Compiled);

        private BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusFileContents)
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = 0;
            ExitCode = 0;

            var match = StatusFileRegex.Match(statusFileContents);

            if (match.Success
                && uint.Parse(match.Groups["gp_x"].Value) != 0
                && uint.Parse(match.Groups["wv"].Value) != 0
                && uint.Parse(match.Groups["gd_x"].Value) != 0)
            {
                var gridX = uint.Parse(match.Groups["gd_x"].Value);
                var gridY = uint.Parse(match.Groups["gd_y"].Value);
                var gridZ = uint.Parse(match.Groups["gd_z"].Value);
                var groupX = uint.Parse(match.Groups["gp_x"].Value);
                var groupY = uint.Parse(match.Groups["gp_y"].Value);
                var groupZ = uint.Parse(match.Groups["gp_z"].Value);
                WaveSize = uint.Parse(match.Groups["wv"].Value);
                NDRange3D = gridY != 0 && gridZ != 0;

                if ((NDRange3D && (groupY == 0 || groupZ == 0 || groupY > gridY || groupZ > gridZ))
                    || groupX > gridX || WaveSize > groupX)
                    throw new ArgumentException();

                GroupSize = groupX;
                DimX = gridX / groupX;
                DimY = NDRange3D ? gridY / groupY : 0;
                DimZ = NDRange3D ? gridZ / groupZ : 0;
                StatusString = match.Groups["comment"].Value;
            }
            else if (!string.IsNullOrEmpty(statusFileContents))
            {
                throw new ArgumentException();
            }
        }

        public static BreakState GetBreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusFileContents)
        {
            try
            {
                return new BreakState(breakStateData, totalElapsedMilliseconds, statusFileContents);
            }
            catch (ArgumentException)
            {
                Errors.ShowWarning("Could not set dispatch parameters from the status file. Make sure that the status file contents match the format.");
                return null;
            }
        }
    }
}
