using System.Collections.Generic;

namespace VSRAD.Deborgar.Server
{
    sealed class ExecutionController
    {
        readonly struct StepState
        {
            internal bool IsStepping { get; }
            internal uint BreakLine { get; }
            internal bool RerunToCurrentBreak { get; }

            internal StepState(bool isStepping, uint breakLine) : this(isStepping, breakLine, false) { }

            private StepState(bool isStepping, uint breakLine, bool rerun)
            {
                IsStepping = isStepping;
                BreakLine = breakLine;
                RerunToCurrentBreak = rerun;
            }

            internal StepState CopyRequestRerun() => new StepState(IsStepping, BreakLine, rerun: true);

            internal StepState CopyExecutionCompleted() => new StepState(IsStepping, BreakLine, rerun: false);
        }

        private readonly IEngineIntegration _engineIntegration;
        private readonly IEngineCallbacks _callbacks;

        private readonly Dictionary<string, StepState> _stepState = new Dictionary<string, StepState>();

        private bool _previousExecutionFailed = false;

        public string CurrentFile { get; private set; }
        public uint CurrentBreakTarget => _stepState[CurrentFile].BreakLine;

        public ExecutionController(IEngineIntegration engineIntegration, IEngineCallbacks callbacks)
        {
            _engineIntegration = engineIntegration;
            _engineIntegration.RerunRequested += RequestRerun;
            _engineIntegration.ExecutionCompleted += ExecutionCompleted;

            _callbacks = callbacks;
        }

        public void ComputeNextBreakTarget(string file, IBreakpointManager breakpointManager)
        {
            if (CurrentFile != null && _stepState.TryGetValue(CurrentFile, out var stepState) && stepState.RerunToCurrentBreak)
                return; /* the next break target is the same as the current one */

            if (_engineIntegration.PopRunToLineIfSet(file, out var breakLine))
            {
                CurrentFile = file;
                _stepState[file] = new StepState(isStepping: false, breakLine);
                return;
            }

            if (CurrentFile == file && _previousExecutionFailed)
                return; /* this is identical to RerunToCurrentBreak but has lower priority than RunToLine */

            var previousBreakLine = _stepState.ContainsKey(file) ? _stepState[file].BreakLine : 0;
            breakLine = breakpointManager.GetNextBreakpointLine(file, previousBreakLine);
            CurrentFile = file;
            _stepState[file] = new StepState(isStepping: false, breakLine);
        }

        public void ComputeNextStepTarget(string file)
        {
            if (CurrentFile == file && _previousExecutionFailed)
                return;

            CurrentFile = file;
            var previousBreakLine = _stepState.ContainsKey(file) ? _stepState[file].BreakLine : 0;
            _stepState[file] = new StepState(isStepping: true, breakLine: previousBreakLine + 1);
        }

        private void RequestRerun()
        {
            _stepState[CurrentFile] = _stepState[CurrentFile].CopyRequestRerun();
        }

        private void ExecutionCompleted(bool success)
        {
            _previousExecutionFailed = !success;
            _stepState[CurrentFile] = _stepState[CurrentFile].CopyExecutionCompleted();

            if (_stepState[CurrentFile].IsStepping)
                _callbacks.OnStepComplete();
            else
                _callbacks.OnBreakComplete();
        }
    }
}
