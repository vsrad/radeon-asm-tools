using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.Server;
using System.Linq;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class DebuggerIntegration
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;

        public event DebugBreakEntered BreakEntered;

        public bool DebugInProgress { get; private set; } = false;

        private Tuple<string, uint> _debugRunToLine;
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

            ThreadHelper.JoinableTaskFactory.Run(_deployManager.ClearSynchronizedFilesAsync);
            DebugInProgress = true;
            return _debugSession;
        }

        public void DeregisterEngine()
        {
            DebugInProgress = false;
            _debugSession = null;
        }

        internal void CreateDebugSession() =>
            _debugSession = new DebugSession(_project, _codeEditor, _channel, _deployManager, _outputWindow,
                _project.Options.DebuggerOptions.GetWatchSnapshot, PopRunToLineIfSet, BreakEntered);

        internal void Rerun()
        {
            _debugSession.RequestRerun();
            Launch();
        }

        internal void RunToCurrentLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectFile = _project.GetRelativePath(_codeEditor.GetAbsoluteSourcePath());
            var line = _codeEditor.GetCurrentLine();
            _debugRunToLine = new Tuple<string, uint>(projectFile, line);

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

        internal bool PopRunToLineIfSet(string projectFile, out uint runToLine)
        {
            if (_debugRunToLine != null && _debugRunToLine.Item1 == projectFile)
            {
                runToLine = _debugRunToLine.Item2;
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
