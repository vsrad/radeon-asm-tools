using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionController
    {
        bool IsActionRunning { get; }

        Task RunActionAsync(string actionName, BreakTargetSelector debugBreakTarget);
        void AbortRunningAction();
    }

    [Export(typeof(IActionController))]
    public sealed class ActionController : IActionController, IActionRunnerCallbacks
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly IDebuggerIntegration _debuggerIntegration;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private readonly SemaphoreSlim _runningActionSemaphore = new SemaphoreSlim(1, 1);
        private string _runningActionName;
        private CancellationTokenSource _runningActionTokenSource;

        public bool IsActionRunning => _runningActionName != null;

        [ImportingConstructor]
        public ActionController(
            IProject project,
            IActionLogger actionLogger,
            ICommunicationChannel channel,
            IProjectSourceManager projectSourceManager,
            IDebuggerIntegration debuggerIntegration,
            IBreakpointTracker breakpointTracker,
            SVsServiceProvider serviceProvider)
        {
            _project = project;
            _actionLogger = actionLogger;
            _channel = channel;
            _projectSourceManager = projectSourceManager;
            _debuggerIntegration = debuggerIntegration;
            _breakpointTracker = breakpointTracker;
            _serviceProvider = serviceProvider;
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public async Task RunActionAsync(string actionName, BreakTargetSelector debugBreakTarget)
        {
            Result<ActionRunResult> actionRun = (ActionRunResult)null;
            bool anotherActionRunning = false;
            bool actionReadsDebugData = false;
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _runningActionSemaphore.WaitAsync();
                try
                {
                    if (_runningActionName != null)
                    {
                        anotherActionRunning = true;
                        return;
                    }
                    _runningActionName = actionName;
                    _runningActionTokenSource = new CancellationTokenSource();
                }
                finally
                {
                    _runningActionSemaphore.Release();
                }

                Options.ActionProfileOptions action;

                if (string.IsNullOrEmpty(actionName))
                    actionRun = new Error("No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                        "You can find command mappings in the Toolbar section.");
                else if ((action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName)) == null)
                    actionRun = new Error($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                        "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");
                else if (!(actionReadsDebugData = _project.Options.Profile.ActionReadsDebugData(action)) && actionName == _project.Options.Profile.MenuCommands.DebugAction)
                    actionRun = new Error($"Action {actionName} is set as the debug action, but does not contain a Read Debug Data step.\r\n\r\n" +
                        "To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.");
                else
                    actionRun = await LaunchActionAsync(action, debugBreakTarget);
            }
            finally
            {
                await _runningActionSemaphore.WaitAsync();
                try
                {
                    if (anotherActionRunning)
                    {
                        var abortAction = VsShellUtilities.ShowMessageBox(_serviceProvider,
                            "Do you want to cancel the current running action?\r\n\r\nPress Yes to abort the action, or No to wait for it to complete.",
                            $"Cannot launch a new action because {_runningActionName} is already running.",
                            OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
                        if (abortAction == (int)VSConstants.MessageBoxResult.IDYES)
                            _runningActionTokenSource.Cancel();
                    }
                    else
                    {
                        _runningActionName = null;
                        _runningActionTokenSource = null;

                        if (actionReadsDebugData)
                            _debuggerIntegration.NotifyDebugActionExecuted(actionRun, debugBreakTarget);

                        var error = await _actionLogger.LogActionRunAsync(actionName, actionRun);
                        if (error != default)
                        {
#pragma warning disable VSTHRD001 // Using BeginInvoke to show the error popup after the Error List window is refreshed
                            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                                (Action)(() => Errors.Show(error)), System.Windows.Threading.DispatcherPriority.ContextIdle);
#pragma warning restore VSTHRD001
                        }
                    }
                }
                finally
                {
                    _runningActionSemaphore.Release();
                }
            }
        }

        public void AbortRunningAction()
        {
            _runningActionSemaphore.Wait();
            try
            {
                _runningActionTokenSource?.Cancel();
            }
            finally
            {
                _runningActionSemaphore.Release();
            }
        }

        private async Task<Result<ActionRunResult>> LaunchActionAsync(Options.ActionProfileOptions action, BreakTargetSelector debugBreakTarget)
        {
            var activeEditor = _projectSourceManager.GetActiveEditorView();
            var (activeFile, activeFileLine) = (activeEditor.GetFilePath(), activeEditor.GetCaretPos().Line);
            var debugFile = _projectSourceManager.DebugStartupPath ?? activeFile;
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            var breakTarget = _breakpointTracker.GetTarget(debugFile, debugBreakTarget);
            var transients = new MacroEvaluatorTransientValues(activeFileLine, activeFile, debugFile, _project.Options.TargetProcessor);

            try
            {
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                _project.Options.DebuggerOptions.UpdateLastAppArgs();

                var projectProperties = _project.GetProjectProperties();
                var remoteEnvironment = _project.Options.Profile.General.RunActionsLocally
                    ? null
                    : new AsyncLazy<IReadOnlyDictionary<string, string>>(() => _channel.GetRemoteEnvironmentAsync(_runningActionTokenSource.Token), ThreadHelper.JoinableTaskFactory);
                var remotePlatform = _project.Options.Profile.General.RunActionsLocally
                    ? System.Runtime.InteropServices.OSPlatform.Windows
                    : await _channel.GetRemotePlatformAsync(_runningActionTokenSource.Token);

                var evaluator = new MacroEvaluator(projectProperties, transients, remoteEnvironment, _project.Options.DebuggerOptions, _project.Options.Profile);

                var generalResult = await _project.Options.Profile.General.EvaluateAsync(evaluator);
                if (!generalResult.TryGetResult(out var general, out var evalError))
                    return evalError;

                var actionTransients = new Options.ActionEvaluationTransients(general.LocalWorkDir, general.RemoteWorkDir, general.RunActionsLocally,
                    remotePlatform, _project.Options.Profile.Actions);
                var evalResult = await action.EvaluateAsync(evaluator, actionTransients);
                if (!evalResult.TryGetResult(out action, out evalError))
                    return evalError;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _projectSourceManager.SaveProjectState();

                var env = new ActionEnvironment(watches, breakTarget);
                var runner = new ActionRunner(_channel, this, env);
                var runResult = await Task.Run(() => runner.RunAsync(action.Name, action.Steps, general.ContinueActionExecOnError, _runningActionTokenSource.Token)).ConfigureAwait(false);
                return runResult;
            }
            finally
            {
                await _statusBar.ClearAsync();
            }
        }

        void IActionRunnerCallbacks.OnOpenFileInEditorRequested(string filePath, string lineMarker)
        {
            VsEditor.OpenFileInEditor(_serviceProvider, filePath, line: null, lineMarker,
                _project.Options.DebuggerOptions.ForceOppositeTab, _project.Options.DebuggerOptions.PreserveActiveDoc);
        }
    }
}
