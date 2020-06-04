namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public BreakStateData Data { get; }
        public long TotalElapsedMilliseconds { get; }
        public long ExecElapsedMilliseconds { get; }
        public string StatusString { get; }
        public int ExitCode { get; }

        public BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, long execElapsedMilliseconds, string statusString, int exitCode)
        {
            Data = breakStateData;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = execElapsedMilliseconds;
            StatusString = statusString;
            ExitCode = exitCode;
        }
    }
}
