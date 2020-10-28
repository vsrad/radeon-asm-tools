using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
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
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly ICommunicationChannel _channel;
        private readonly IActionLogger _actionLogger;
        private readonly IBreakpointTracker _breakpointTracker;

        public bool DebugInProgress { get; private set; } = false;

        private DebugSession _debugSession;

        [ImportingConstructor]
        public DebuggerIntegration(
            IProject project,
            SVsServiceProvider serviceProvider,
            IActiveCodeEditor codeEditor,
            IFileSynchronizationManager deployManager,
            ICommunicationChannel channel,
            IActionLogger actionLogger,
            IBreakpointTracker breakpointTracker)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            _codeEditor = codeEditor;
            _deployManager = deployManager;
            _channel = channel;
            _actionLogger = actionLogger;
            _breakpointTracker = breakpointTracker;
        }

        public IEngineIntegration RegisterEngine()
        {
            if (_debugSession == null)
                throw new InvalidOperationException($"{nameof(RegisterEngine)} must only be called by the engine, and the engine must be launched via {nameof(DebuggerLaunchProvider)}");

            DebugInProgress = true;
            return this;
        }

        public void DeregisterEngine()
        {
            DebugInProgress = false;
            _debugSession = null;
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

            _debugSession = new DebugSession(_project, _channel, _deployManager, _serviceProvider);
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
                    var result = await _debugSession.ExecuteAsync(transients);
                    await VSPackage.TaskFactory.SwitchToMainThreadAsync();

                    if (result.ActionResult != null)
                    {
                        var actionError = await _actionLogger.LogActionWithWarningsAsync(result.ActionResult);
                        if (actionError is Error e1)
                            Errors.Show(e1);
                    }

                    if (result.Error is Error e2)
                        Errors.Show(e2);

                    RaiseExecutionCompleted(file, breakLines, step, result.BreakState);
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
            var args = new ExecutionCompletedEventArgs(file, lines, isStepping, isSuccessful: breakState != null);
            ExecutionCompleted(this, args);
            BreakEntered(breakState);
        }
    }
}
