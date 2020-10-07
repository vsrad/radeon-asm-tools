using System;
using System.Text.RegularExpressions;
using VSRAD.Package.Utils;

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
        public bool StatusFileSet { get; }

        private static readonly Regex StatusFileRegex = new Regex(@"grid size \((?<gd_x>\d+), (?<gd_y>\d+), (?<gd_z>\d+)\)\s+group size \((?<gp_x>\d+), (?<gp_y>\d+), (?<gp_z>\d+)\)\s+wave size (?<wv>\d+)\s+comment (?<comment>.+)", RegexOptions.Compiled);
       
        private BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, bool statusFileSet,
            uint waveSize = 0, uint dimX = 0, uint dimY = 0, uint dimZ = 0, uint groupSize = 0, bool ndRange3d = false, string status = "")
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = 0;
            ExitCode = 0;
            StatusFileSet = statusFileSet;

            if (statusFileSet)
            {
                WaveSize = waveSize;
                DimX = dimX;
                DimY = dimY;
                DimZ = dimZ;
                GroupSize = groupSize;
                NDRange3D = ndRange3d;
                StatusString = status;
            }
        }

        public static Result<BreakState> Create(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusFileContents)
        {
            var match = StatusFileRegex.Match(statusFileContents);

            if (!match.Success)
            {
                if (!string.IsNullOrEmpty(statusFileContents))
                    return new Error("Could not set dispatch parameters from the status file. Make sure that the status file contents match the format.");
                return new BreakState(breakStateData, totalElapsedMilliseconds, statusFileSet: false);
            }

            var gridX = uint.Parse(match.Groups["gd_x"].Value);
            var gridY = uint.Parse(match.Groups["gd_y"].Value);
            var gridZ = uint.Parse(match.Groups["gd_z"].Value);
            var groupX = uint.Parse(match.Groups["gp_x"].Value);
            var groupY = uint.Parse(match.Groups["gp_y"].Value);
            var groupZ = uint.Parse(match.Groups["gp_z"].Value);
            var waveSize = uint.Parse(match.Groups["wv"].Value);
            var ndRange3D = gridY != 0 && gridZ != 0;
            var statusString = match.Groups["comment"].Value;

            if (ndRange3D && (groupY == 0 || groupZ == 0))
                return new Error("Could not set dispatch parameters from the status file. If GridY and GridZ is set, GroupY and GroupZ cannot be zero.");

            if (ndRange3D && (groupY > gridY || groupZ > gridZ))
                return new Error("Could not set dispatch parameters from the status file. If GridY and GridZ is set, GroupY and GroupZ cannot be bigger than correspond Grid value.");

            if (groupX > gridX)
                return new Error("Could not set dispatch parameters from the status file. GroupX cannot be bigger than GridX.");

            if (waveSize > groupX)
                return new Error("Could not set dispatch parameters from the status file. WaveSize cannot be bigger than GroupX.");

            if (groupX == 0)
                return new Error("Could not set dispatch parameters from the status file. GroupX cannot be zero.");

            if (gridX == 0)
                return new Error("Could not set dispatch parameters from the status file. GridX cannot be zero.");

            if (waveSize == 0)
                return new Error("Could not set dispatch parameters from the status file. WaveSize cannot be zero.");

            var dimX = gridX / groupX;
            var dimY = ndRange3D ? gridY / groupY : 0;
            var dimZ = ndRange3D ? gridZ / groupZ : 0;

            return new BreakState(breakStateData, totalElapsedMilliseconds, statusFileSet: true, waveSize, dimX, dimY, dimZ, groupX, ndRange3D, statusString);
        }
    }
}
