using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Deborgar;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void DebugBreakEntered(BreakState breakState);

    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class DebuggerIntegration : IEngineIntegration
    {
        public event ExecutionCompleted ExecutionCompleted;
        public event DebugBreakEntered BreakEntered;

        private readonly SVsServiceProvider _serviceProvider;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;

        public bool DebugInProgress { get; private set; } = false;

        private (string file, uint line)? _debugRunToLine;
        private DebugSession _debugSession;
        private IProject _project;

        [ImportingConstructor]
        public DebuggerIntegration(
            SVsServiceProvider serviceProvider,
            IActiveCodeEditor codeEditor,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel)
        {
            _serviceProvider = serviceProvider;
            _codeEditor = codeEditor;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;

            DebugEngine.InitializationCallback = RegisterEngine;
            DebugEngine.TerminationCallback = DeregisterEngine;
        }

        /* DebuggerIntegration and Project are circular dependencies,
         * so we use this hack to obtain the project once it's created. */
        public void SetProjectOnLoad(IProject project) => _project = project;

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

        internal void CreateDebugSession() =>
            _debugSession = new DebugSession(_project, _channel, _deployManager, _outputWindow);

        internal void RunToCurrentLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectFile = _project.GetRelativePath(_codeEditor.GetAbsoluteSourcePath());
            var line = _codeEditor.GetCurrentLine();
            _debugRunToLine = (projectFile, line);

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
                if (result.TryGetResult(out var breakState, out var error))
                {
                    ExecutionCompleted(success: true);
                    BreakEntered(breakState);
                }
                else
                {
                    ExecutionCompleted(success: false);
                    Errors.Show(error);
                }
            },
            exceptionCallbackOnMainThread: () => ExecutionCompleted(success: false));
        }

        string IEngineIntegration.GetActiveProjectFile() =>
            _project.GetRelativePath(_codeEditor.GetAbsoluteSourcePath());

        uint IEngineIntegration.GetFileLineCount(string projectFilePath) =>
            (uint)File.ReadLines(_project.GetAbsolutePath(projectFilePath)).Count();

        string IEngineIntegration.GetProjectRelativePath(string path) =>
            _project.GetRelativePath(path);

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
    }
}
