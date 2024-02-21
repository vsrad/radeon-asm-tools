using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionController
    {
        bool IsActionRunning { get; }

        Task<Result<ActionRunResult>> RunActionAsync(string actionName, BreakTargetSelector debugBreakTarget);
        void AbortRunningAction();
    }

    [Export(typeof(IActionController))]
    public sealed class ActionController : IActionController
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly IDebuggerIntegration _debuggerIntegration;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private readonly object _runningActionLock = new object();
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

        public async Task<Result<ActionRunResult>> RunActionAsync(string actionName, BreakTargetSelector debugBreakTarget)
        {
            if (string.IsNullOrEmpty(actionName))
                return new Error("No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "You can find command mappings in the Toolbar section.");

            var action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                return new Error($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");

            bool actionReadsDebugData = _project.Options.Profile.ActionReadsDebugData(action);
            if (actionName == _project.Options.Profile.MenuCommands.DebugAction && !actionReadsDebugData)
                return new Error($"Action {actionName} is set as the debug action, but does not contain a Read Debug Data step.\r\n\r\n" +
                    "To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.");

            lock (_runningActionLock)
            {
                if (_runningActionName != null)
                    return new Error($"Action {_runningActionName} is already running.\r\n\r\n" +
                        "You may abort it by clicking on the \"Abort Running Action\" icon in the RAD Debug toolbar.");
                _runningActionName = action.Name;
                _runningActionTokenSource = new CancellationTokenSource();
            }
            ActionRunResult runResult = null;
            try
            {
                var launchResult = await LaunchActionAsync(action, debugBreakTarget);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (launchResult.TryGetResult(out runResult, out var error) && runResult != null)
                    await _actionLogger.LogActionRunAsync(runResult);
                return launchResult;
            }
            finally
            {
                if (actionReadsDebugData)
                    _debuggerIntegration.NotifyDebugActionExecuted(runResult, debugBreakTarget);

                lock (_runningActionLock)
                {
                    _runningActionName = null;
                    _runningActionTokenSource = null;
                }
            }
        }

        public void AbortRunningAction()
        {
            lock (_runningActionLock)
            {
                _runningActionTokenSource?.Cancel();
            }
        }

        private async Task<Result<ActionRunResult>> LaunchActionAsync(Options.ActionProfileOptions action, BreakTargetSelector debugBreakTarget)
        {
            var activeEditor = _projectSourceManager.GetActiveEditorView();
            var (activeFile, activeFileLine) = (activeEditor.GetFilePath(), activeEditor.GetCaretPos().Line);
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            var breakTarget = _breakpointTracker.GetTarget(activeFile, debugBreakTarget);
            var transients = new MacroEvaluatorTransientValues(activeFileLine, activeFile, _project.Options.TargetProcessor);

            try
            {
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                _project.Options.DebuggerOptions.UpdateLastAppArgs();

                var projectProperties = _project.GetProjectProperties();
                var remoteEnvironment = _project.Options.Profile.General.RunActionsLocally
                    ? null
                    : new AsyncLazy<IReadOnlyDictionary<string, string>>(_channel.GetRemoteEnvironmentAsync, ThreadHelper.JoinableTaskFactory);

                var evaluator = new MacroEvaluator(projectProperties, transients, remoteEnvironment, _project.Options.DebuggerOptions, _project.Options.Profile);

                var generalResult = await _project.Options.Profile.General.EvaluateAsync(evaluator);
                if (!generalResult.TryGetResult(out var general, out var evalError))
                    return evalError;
                var evalResult = await action.EvaluateAsync(evaluator, _project.Options.Profile);
                if (!evalResult.TryGetResult(out action, out evalError))
                    return evalError;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _projectSourceManager.SaveProjectState();

                var continueOnError = _project.Options.Profile.General.ContinueActionExecOnError;
                var checkMagicNumber = _project.Options.VisualizerOptions.CheckMagicNumber ? (uint?)_project.Options.VisualizerOptions.MagicNumber : null;
                var env = new ActionEnvironment(general.LocalWorkDir, general.RemoteWorkDir, watches, breakTarget, checkMagicNumber);
                var runner = new ActionRunner(_channel, _serviceProvider, env, _project);
                var runResult = await runner.RunAsync(action.Name, action.Steps, continueOnError, _runningActionTokenSource.Token).ConfigureAwait(false);
                return runResult;
            }
            finally
            {
                await _statusBar.ClearAsync();
            }
        }
    }
}
