namespace VSRAD.Package.Server
{
    public sealed class DebugRunResult
    {
        public ActionRunResult ActionResult { get; }

        public Error? Error { get; }

        public BreakState SuccessfulState { get; }

        public DebugRunResult(ActionRunResult actionResult, Error? error, BreakState successfulState)
        {
            ActionResult = actionResult;
            Error = error;
            SuccessfulState = successfulState;
        }
    }
}
