using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
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
            var file = _codeEditor.GetAbsoluteSourcePath();
            var target = _breakpointTracker.MoveToNextBreakTarget(file, step);
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var result = await _debugSession.ExecuteAsync(target.Lines, watches);
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();

                if (result.ActionResult != null)
                {
                    var actionError = await _actionLogger.LogActionWithWarningsAsync(result.ActionResult);
                    if (actionError is Error e1)
                        Errors.Show(e1);
                }

                if (result.Error is Error e2)
                    Errors.Show(e2);

                RaiseExecutionCompleted(target, result.BreakState);
            },
            exceptionCallbackOnMainThread: () => RaiseExecutionCompleted(target, null));
        }

        string IEngineIntegration.GetActiveSourcePath() =>
            _codeEditor.GetAbsoluteSourcePath();

        private void RaiseExecutionCompleted(BreakTarget target, BreakState breakState)
        {
            var args = new ExecutionCompletedEventArgs(target, isSuccessful: breakState != null);
            ExecutionCompleted(this, args);
            BreakEntered(breakState);
        }
    }
}
