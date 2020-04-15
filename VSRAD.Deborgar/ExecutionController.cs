using System.Collections.Generic;

namespace VSRAD.Deborgar.Server
{
    sealed class ExecutionController
    {
        private readonly IEngineIntegration _engineIntegration;
        private readonly IEngineCallbacks _callbacks;

        private readonly Dictionary<string, (bool isStepping, uint[] breakLines)> _stepState =
            new Dictionary<string, (bool isStepping, uint[] breakLines)>();

        public string CurrentFile { get; private set; }
        public uint[] CurrentBreakTarget => _stepState[CurrentFile].breakLines;

        public ExecutionController(IEngineIntegration engineIntegration, IEngineCallbacks callbacks)
        {
            _engineIntegration = engineIntegration;
            _engineIntegration.ExecutionCompleted += ExecutionCompleted;

            _callbacks = callbacks;
        }

        public void ComputeNextBreakTarget(string file, IBreakpointManager breakpointManager)
        {
            CurrentFile = file;
            _stepState[file] = (isStepping: false, ComputeBreakLines(file, breakpointManager));
        }

        private uint[] ComputeBreakLines(string file, IBreakpointManager breakpointManager)
        {
            if (_engineIntegration.PopRunToLineIfSet(file, out var breakLine))
                return new[] { breakLine };
            switch (_engineIntegration.GetBreakMode())
            {
                case BreakMode.Multiple:
                    return breakpointManager.GetBreakpointLines(file);
                case BreakMode.SingleRerun:
                    if (_stepState.TryGetValue(file, out var prevState))
                        return new[] { prevState.breakLines[0] };
                    return new[] { breakpointManager.GetNextBreakpointLine(file, 0) };
                default:
                    var prevBreakLine = _stepState.TryGetValue(file, out prevState) ? prevState.breakLines[0] : 0;
                    return new[] { breakpointManager.GetNextBreakpointLine(file, prevBreakLine) };
            }
        }

        public void ComputeNextStepTarget(string file)
        {
            CurrentFile = file;
            if (_stepState.TryGetValue(file, out var prevState))
            {
                if (_engineIntegration.GetBreakMode() == BreakMode.Multiple)
                    _stepState[file] = (isStepping: true, prevState.breakLines);
                else
                    _stepState[file] = (isStepping: true, breakLines: new[] { prevState.breakLines[0] + 1 });
            }
            else
            {
                _stepState[file] = (isStepping: true, breakLines: new[] { 0u });
            }
        }

        private void ExecutionCompleted(bool success)
        {
            if (_stepState[CurrentFile].isStepping)
                _callbacks.OnStepComplete();
            else
                _callbacks.OnBreakComplete();
        }
    }
}
