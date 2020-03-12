using System.Collections.Generic;

namespace VSRAD.Deborgar.Server
{
    sealed class ExecutionController
    {
        private readonly IEngineIntegration _engineIntegration;
        private readonly IEngineCallbacks _callbacks;

        private readonly Dictionary<string, (bool isStepping, uint breakLine)> _stepState =
            new Dictionary<string, (bool isStepping, uint breakLine)>();

        public string CurrentFile { get; private set; }
        public uint CurrentBreakTarget => _stepState[CurrentFile].breakLine;

        public ExecutionController(IEngineIntegration engineIntegration, IEngineCallbacks callbacks)
        {
            _engineIntegration = engineIntegration;
            _engineIntegration.ExecutionCompleted += ExecutionCompleted;

            _callbacks = callbacks;
        }

        public void ComputeNextBreakTarget(string file, IBreakpointManager breakpointManager)
        {
            CurrentFile = file;
            if (!_engineIntegration.PopRunToLineIfSet(file, out var breakLine))
            {
                var prevBreakLine = _stepState.TryGetValue(file, out var prevState) ? prevState.breakLine : 0;
                breakLine = breakpointManager.GetNextBreakpointLine(file, prevBreakLine);
            }
            _stepState[file] = (isStepping: false, breakLine);
        }

        public void ComputeNextStepTarget(string file)
        {
            CurrentFile = file;
            var prevBreakLine = _stepState.TryGetValue(file, out var prevState) ? prevState.breakLine : 0;
            _stepState[file] = (isStepping: true, breakLine: prevBreakLine + 1);
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
