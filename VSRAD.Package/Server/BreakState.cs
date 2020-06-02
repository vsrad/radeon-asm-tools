namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public BreakStateData Data { get; }

        public long TotalElapsedMilliseconds { get; }
        public long ExecElapsedMilliseconds { get; }
        public string StatusString { get; }

        public BreakState(BreakStateData breakStateData, long totalElapsedMilliseconds, long execElapsedMilliseconds, string statusString)
        {
            Data = breakStateData;
            StatusString = statusString;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            ExecElapsedMilliseconds = execElapsedMilliseconds;
        }
    }
}
