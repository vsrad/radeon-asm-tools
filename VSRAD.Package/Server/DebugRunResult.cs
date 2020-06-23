namespace VSRAD.Package.Server
{
    public sealed class DebugRunResult
    {
        public ActionRunResult ActionResult { get; }

        public Error? Error { get; }

        public BreakState BreakState { get; }

        public DebugRunResult(ActionRunResult actionResult, Error? error, BreakState breakState)
        {
            ActionResult = actionResult;
            Error = error;
            BreakState = breakState;
        }
    }
}
