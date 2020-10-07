using System;

namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public BreakStateData Data { get; }
        public BreakStateDispatchParameters DispatchParameters { get; }
        public long TotalElapsedMilliseconds { get; }
        public long ExecElapsedMilliseconds { get; } = 0; // TODO: is this obsolete?
        public int ExitCode { get; } = 0; // TODO: is this obsolete?
        public DateTime ExecutedAt { get; } = DateTime.Now;

        public BreakState(BreakStateData breakStateData, BreakStateDispatchParameters dispatchParameters, long totalElapsedMilliseconds)
        {
            Data = breakStateData;
            DispatchParameters = dispatchParameters;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
        }
    }
}
