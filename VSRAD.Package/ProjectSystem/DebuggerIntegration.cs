using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void DebugBreakEntered(BreakState breakState);

    [Export]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebuggerIntegration : IEngineIntegration
    {
        public event DebugBreakEntered BreakEntered;

        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        private readonly IProject _project;
        private readonly IActionLauncher _actionLauncher;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IBreakpointTracker _breakpointTracker;

        public bool DebugInProgress { get; private set; } = false;

        [ImportingConstructor]
        public DebuggerIntegration(
            IProject project,
            IActionLauncher actionLauncher,
            IActiveCodeEditor codeEditor,
            ICommunicationChannel channel,
            IActionLogger actionLogger,
            IBreakpointTracker breakpointTracker)
        {
            _project = project;
            _actionLauncher = actionLauncher;
            _codeEditor = codeEditor;
            _breakpointTracker = breakpointTracker;
        }

        public IEngineIntegration RegisterEngine()
        {
            if (DebugInProgress)
                throw new InvalidOperationException($"{nameof(RegisterEngine)} must only be called by the engine, and the engine must be launched via {nameof(DebuggerLaunchProvider)}");

            DebugInProgress = true;
            return this;
        }

        public void DeregisterEngine()
        {
            DebugInProgress = false;
            // unsubscribe event listeners on the debug engine (VSRAD.Deborgar) side, otherwise we'd get ghost debug sessions
            ExecutionCompleted = null;
        }

        internal bool TryCreateDebugSession()
        {
            if (!_project.Options.HasProfiles)
            {
                Errors.ShowProfileUninitializedError();
                return false;
            }

            DebugEngine.InitializationCallback = RegisterEngine;
            DebugEngine.TerminationCallback = DeregisterEngine;

            return true;
        }

        void IEngineIntegration.Execute(bool step)
        {
            try
            {
                var (file, breakLines) = _breakpointTracker.MoveToNextBreakTarget(step);
                var line = _codeEditor.GetCurrentLine();
                var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
                VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
                {
                    var transients = new MacroEvaluatorTransientValues(line, file, breakLines, watches);
                    var result = await _actionLauncher.LaunchActionByNameAsync(ActionProfileOptions.BuiltinActionDebug, transients);
                    await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                    RaiseExecutionCompleted(file, breakLines, step, result?.BreakState);
                },
                exceptionCallbackOnMainThread: () => RaiseExecutionCompleted(file, breakLines, step, null));
            }
            catch (Exception e)
            {
                Errors.ShowException(e);
                RaiseExecutionCompleted("", new[] { 0u }, step, null);
            }
        }

        void IEngineIntegration.CauseBreak()
        {
            string file = "";
            // May throw an exception if no files are open in the editor
            try { file = _codeEditor.GetAbsoluteSourcePath(); } catch { }

            RaiseExecutionCompleted(file, new[] { 0u }, isStepping: false, null);
        }

        private void RaiseExecutionCompleted(string file, uint[] lines, bool isStepping, BreakState breakState)
        {
            var args = new ExecutionCompletedEventArgs(file, lines, isStepping);
            ExecutionCompleted?.Invoke(this, args);
            BreakEntered(this, breakState);
        }
    }
}
