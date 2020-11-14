using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebuggerIntegration : IEngineIntegration
    {
        public event EventHandler<BreakState> BreakEntered;
        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        private readonly IProject _project;
        private readonly IActionLauncher _actionLauncher;
        private readonly IActiveCodeEditor _codeEditor;

        public bool DebugInProgress { get; private set; } = false;

        [ImportingConstructor]
        public DebuggerIntegration(IProject project, IActionLauncher actionLauncher, IActiveCodeEditor codeEditor)
        {
            _project = project;
            _actionLauncher = actionLauncher;
            _codeEditor = codeEditor;
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

        public bool TryCreateDebugSession()
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

        public void NotifyDebugActionExecuted(ActionRunResult runResult, MacroEvaluatorTransientValues transients)
        {
            RaiseExecutionCompleted(transients?.ActiveSourceFullPath ?? "", transients?.BreakLines ?? new[] { 0u }, isStepping: false, runResult?.BreakState);
        }

        void IEngineIntegration.Execute(bool step)
        {
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var result = await _actionLauncher.LaunchActionByNameAsync(
                    _project.Options.Profile.MenuCommands.DebugAction,
                    moveToNextDebugTarget: true,
                    isDebugSteppingEnabled: step);

                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                if (result.Error is Error e)
                    Errors.Show(e);
                NotifyDebugActionExecuted(result.RunResult, result.Transients);
            },
            exceptionCallbackOnMainThread: () => NotifyDebugActionExecuted(null, null));
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
