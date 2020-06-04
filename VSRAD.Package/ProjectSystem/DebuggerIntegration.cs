using EnvDTE;
using EnvDTE80;
using Microsoft;
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
        public event ExecutionCompleted ExecutionCompleted;
        public event DebugBreakEntered BreakEntered;

        private readonly IProject _project;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;
        private readonly IErrorListManager _errorListManager;

        public bool DebugInProgress { get; private set; } = false;

        private (string file, uint line)? _debugRunToLine;
        private DebugSession _debugSession;

        [ImportingConstructor]
        public DebuggerIntegration(
            IProject project,
            SVsServiceProvider serviceProvider,
            IActiveCodeEditor codeEditor,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            IErrorListManager errorListManager)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            _codeEditor = codeEditor;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
            _errorListManager = errorListManager;

            DebugEngine.InitializationCallback = RegisterEngine;
            DebugEngine.TerminationCallback = DeregisterEngine;
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

            _debugSession = new DebugSession(_project, _channel, _deployManager, _outputWindow, _errorListManager);
            return true;
        }

        internal void RunToCurrentLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sourcePath = _codeEditor.GetAbsoluteSourcePath();
            var line = _codeEditor.GetCurrentLine();
            _debugRunToLine = (sourcePath, line);

            Launch();
        }

        private void Launch()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            Assumes.Present(dte);

            if (dte.Debugger.CurrentMode != dbgDebugMode.dbgRunMode) // Go() must not be invoked when the debugger is already running (not in break mode)
                dte.Debugger.Go();
        }

        void IEngineIntegration.Execute(uint[] breakLines)
        {
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var result = await _debugSession.ExecuteAsync(breakLines, watches);
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();

                if (!result.TryGetResult(out var breakState, out var error))
                    Errors.Show(error);
                else if (breakState.ExitCode != 0)
                    Errors.ShowWarning(RemoteCommandExecutor.ErrorNonZeroExitCode("RAD Debugger", breakState.ExitCode));

                RaiseExecutionCompleted(breakState);
            },
            exceptionCallbackOnMainThread: () => RaiseExecutionCompleted(null));
        }

        string IEngineIntegration.GetActiveSourcePath() =>
            _codeEditor.GetAbsoluteSourcePath();

        BreakMode IEngineIntegration.GetBreakMode() =>
            _project.Options.DebuggerOptions.BreakMode;

        bool IEngineIntegration.PopRunToLineIfSet(string file, out uint runToLine)
        {
            if (_debugRunToLine.HasValue && _debugRunToLine.Value.file == file)
            {
                runToLine = _debugRunToLine.Value.line;
                _debugRunToLine = null;
                return true;
            }
            else
            {
                runToLine = 0;
                return false;
            }
        }

        private void RaiseExecutionCompleted(BreakState breakState)
        {
            ExecutionCompleted(success: breakState != null);
            BreakEntered(breakState);
        }
    }
}
