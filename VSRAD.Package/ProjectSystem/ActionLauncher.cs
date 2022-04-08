using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public sealed class ActionCompletedEventArgs : EventArgs
    {
        public Error? Error { get; }
        public ActionProfileOptions Action { get; }
        public MacroEvaluatorTransientValues Transients { get; }
        public ActionRunResult RunResult { get; }

        public ActionCompletedEventArgs(Error? error, ActionProfileOptions action, MacroEvaluatorTransientValues transients, ActionRunResult runResult = null)
        {
            Error = error;
            Action = action;
            Transients = transients;
            RunResult = runResult;
        }
    }

    public enum ActionExecutionState
    {
        Started, Finished, Cancelling, Idle
    }

    public sealed class ActionExecutionStateChangedEventArgs : EventArgs
    {
        public ActionExecutionState State { get; }
        public string ActionName { get; }

        public ActionExecutionStateChangedEventArgs(ActionExecutionState state, string actionName)
        {
            State = state;
            ActionName = actionName;
        }
    }

    public interface IActionLauncher
    {
        Error? TryLaunchActionByName(string actionName, bool moveToNextDebugTarget = false, bool isDebugSteppingEnabled = false);
        void CancelRunningAction();
        bool IsDebugAction(ActionProfileOptions action);

        event EventHandler<ActionCompletedEventArgs> ActionCompleted;
        event EventHandler<ActionExecutionStateChangedEventArgs> ActionExecutionStateChanged;
    }

    [Export(typeof(IActionLauncher))]
    public sealed class ActionLauncher : IActionLauncher, IActionRunController, IDisposable
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IProjectSourceManager _projectSources;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly SVsServiceProvider _serviceProvider;

        public event EventHandler<ActionCompletedEventArgs> ActionCompleted;
        public event EventHandler<ActionExecutionStateChangedEventArgs> ActionExecutionStateChanged;

        private readonly AsyncQueue<(ActionProfileOptions, MacroEvaluatorTransientValues)> _pendingActions =
            new AsyncQueue<(ActionProfileOptions, MacroEvaluatorTransientValues)>();
        private readonly CancellationTokenSource _actionLoopCts = new CancellationTokenSource();

        private string _currentlyRunningActionName;
        private CancellationTokenSource _actionCancellationTokenSource;

        CancellationToken IActionRunController.CancellationToken => _actionCancellationTokenSource.Token;

        [ImportingConstructor]
        public ActionLauncher(
            IProject project,
            IActionLogger actionLogger,
            ICommunicationChannel channel,
            IProjectSourceManager projectSources,
            IActiveCodeEditor codeEditor,
            IBreakpointTracker breakpointTracker,
            SVsServiceProvider serviceProvider)
        {
            _project = project;
            _actionLogger = actionLogger;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _projectSources = projectSources;
            _codeEditor = codeEditor;
            _breakpointTracker = breakpointTracker;
            _project.RunWhenLoaded((_) => VSPackage.TaskFactory.RunAsyncWithErrorHandling(RunActionLoopAsync));
        }

        public void Dispose()
        {
            _actionLoopCts.Cancel();
        }

        public Error? TryLaunchActionByName(string actionName, bool moveToNextDebugTarget = false, bool isDebugSteppingEnabled = false)
        {
            if (string.IsNullOrEmpty(actionName))
                return new Error(
                    "No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "You can find command mappings in the Toolbar section.");

            var action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                return new Error(
                    $"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");

            if (moveToNextDebugTarget && !IsDebugAction(action))
                return new Error(
                    $"Action {actionName} is set as the debug action, but does not contain a Read Debug Data step.\r\n\r\n" +
                    "To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.");

            if (_currentlyRunningActionName != null)
            {
                var shouldCancel = MessageBox.Show($"Action {_currentlyRunningActionName} is already running.\r\n\r\nDo you want to cancel it?",
                    "RAD Debugger", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (shouldCancel != MessageBoxResult.Yes)
                    return null;
                CancelRunningAction();
            }

            var (file, breakLines) = moveToNextDebugTarget
                ? _breakpointTracker.MoveToNextBreakTarget(isDebugSteppingEnabled)
                : _breakpointTracker.GetBreakTarget();
            var line = _codeEditor.GetCurrentLine();
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            var transients = new MacroEvaluatorTransientValues(line, file, breakLines, watches);

            _pendingActions.Enqueue((action, transients));
            return null;
        }

        public void CancelRunningAction()
        {
            if (_currentlyRunningActionName != null && !_actionCancellationTokenSource.IsCancellationRequested)
            {
                ActionExecutionStateChanged?.Invoke(this, new ActionExecutionStateChangedEventArgs(ActionExecutionState.Cancelling, _currentlyRunningActionName));
                _actionCancellationTokenSource.Cancel();
            }
        }

        private async Task RunActionLoopAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync(); // Events need to be fired on the main thread; don't use ConfigureAwait(false) here
            while (!_actionLoopCts.IsCancellationRequested)
            {
                ActionExecutionStateChanged?.Invoke(this, new ActionExecutionStateChangedEventArgs(ActionExecutionState.Idle, null));
                var (action, transients) = await _pendingActions.DequeueAsync(_actionLoopCts.Token);
                try
                {
                    _actionCancellationTokenSource = new CancellationTokenSource();
                    _currentlyRunningActionName = action.Name;
                    ActionExecutionStateChanged?.Invoke(this, new ActionExecutionStateChangedEventArgs(ActionExecutionState.Started, action.Name));

                    var result = await RunActionAsync(action, transients);
                    ActionCompleted?.Invoke(this, result);
                }
                catch (Exception e)
                {
                    Errors.ShowException(e);
                    ActionCompleted?.Invoke(this, new ActionCompletedEventArgs(null, action, transients, null));
                }
                finally
                {
                    _currentlyRunningActionName = null;
                    ActionExecutionStateChanged?.Invoke(this, new ActionExecutionStateChangedEventArgs(ActionExecutionState.Finished, action.Name));
                }
            }
        }

        private async Task<ActionCompletedEventArgs> RunActionAsync(ActionProfileOptions action, MacroEvaluatorTransientValues transients)
        {
            var projectProperties = _project.GetProjectProperties();
            var remoteEnvironment = _project.Options.Profile.General.RunActionsLocally
                ? null
                : new AsyncLazy<IReadOnlyDictionary<string, string>>(_channel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);
            var serverCapabilities = _project.Options.Profile.General.RunActionsLocally
                ? null
                : await _channel.GetServerCapabilityInfoAsync(_actionCancellationTokenSource.Token);

            var evaluator = new MacroEvaluator(projectProperties, transients, remoteEnvironment, _project.Options.DebuggerOptions, _project.Options.Profile);

            var generalResult = await _project.Options.Profile.General.EvaluateAsync(evaluator);
            if (!generalResult.TryGetResult(out var general, out var evalError))
                return new ActionCompletedEventArgs(evalError, action, transients);

            var actionEvalEnv = new ActionEvaluationEnvironment(general.LocalWorkDir, general.RemoteWorkDir, general.RunActionsLocally,
                serverCapabilities, _project.Options.Profile.Actions);
            var actionEvalResult = await action.EvaluateAsync(evaluator, actionEvalEnv);
            if (!actionEvalResult.TryGetResult(out action, out evalError))
                return new ActionCompletedEventArgs(evalError, action, transients);

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            _project.Options.DebuggerOptions.UpdateLastAppArgs();
            _projectSources.SaveProjectState();

            var runner = new ActionRunner(_channel, this, transients.Watches);
            var runResult = await runner.RunAsync(action.Name, action.Steps, _project.Options.Profile.General.ContinueActionExecOnError).ConfigureAwait(false);
            var actionError = await _actionLogger.LogActionWithWarningsAsync(runResult).ConfigureAwait(false);

            return new ActionCompletedEventArgs(actionError, action, transients, runResult);
        }

        public bool IsDebugAction(ActionProfileOptions action)
        {
            foreach (var step in action.Steps)
            {
                if (step is ReadDebugDataStep)
                    return true;
                if (step is RunActionStep runAction
                    && _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == runAction.Name) is ActionProfileOptions nestedAction
                    && IsDebugAction(nestedAction))
                    return true;
            }
            return false;
        }

        async Task<bool> IActionRunController.ShouldTerminateProcessOnTimeoutAsync(IList<ProcessTreeItem> processTree)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var message = new StringBuilder("Execution timeout was reached when running ");
            message.Append(_currentlyRunningActionName);
            message.Append(" action. The following processes are still running:\r\n");
            ProcessUtils.PrintProcessTree(message, processTree);
            message.Append("\r\nDo you want to terminate these processes? Choose No to wait for the processes to finish.");

            var result = MessageBox.Show(message.ToString(), "RAD Debugger", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        async Task IActionRunController.OpenFileInVsEditorAsync(string path, string lineMarker)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            VsEditor.OpenFileInEditor(_serviceProvider, path, lineMarker,
                _project.Options.DebuggerOptions.ForceOppositeTab, _project.Options.DebuggerOptions.PreserveActiveDoc);
        }
    }
}
