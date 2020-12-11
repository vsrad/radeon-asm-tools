using System;

namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public BreakStateData Data { get; }
        public BreakStateDispatchParameters DispatchParameters { get; }
        public DateTime ExecutedAt { get; } = DateTime.Now;

        public BreakState(BreakStateData breakStateData, BreakStateDispatchParameters dispatchParameters)
        {
            Data = breakStateData;
            DispatchParameters = dispatchParameters;
        }
    }
}
