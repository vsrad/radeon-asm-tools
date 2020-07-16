using System;

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

        public BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, string statusString)
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = 0;
            StatusString = statusString;
            ExitCode = 0;
        }
    }
}
