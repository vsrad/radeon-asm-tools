using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public sealed class ActionExecution
    {
        public Error? Error { get; }
        public MacroEvaluatorTransientValues Transients { get; }
        public ActionRunResult RunResult { get; }

        public ActionExecution(Error? error, MacroEvaluatorTransientValues transients = null, ActionRunResult runResult = null)
        {
            Error = error;
            Transients = transients;
            RunResult = runResult;
        }
    }

    public interface IActionLauncher
    {
        Task<ActionExecution> LaunchActionByNameAsync(string actionName, BreakTargetSelector debugBreakTarget = BreakTargetSelector.Last);
        bool IsDebugAction(ActionProfileOptions action);
    }

    [Export(typeof(IActionLauncher))]
    public sealed class ActionLauncher : IActionLauncher
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IProjectSourceManager _projectSources;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private string _currentlyRunningActionName;

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
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public async Task<ActionExecution> LaunchActionByNameAsync(string actionName, BreakTargetSelector debugBreakTarget = BreakTargetSelector.Last)
        {
            if (_currentlyRunningActionName != null)
                return new ActionExecution(new Error(
                    $"Action {_currentlyRunningActionName} is already running.\r\n\r\n" +
                    "If you believe this to be a hang, use the Disconnect button available in Tools -> RAD Debug -> Options to abort the currently running action."));

            if (string.IsNullOrEmpty(actionName))
                return new ActionExecution(new Error(
                    "No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "You can find command mappings in the Toolbar section."));

            var action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                return new ActionExecution(new Error(
                    $"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile."));

            if (debugBreakTarget != BreakTargetSelector.Last && !IsDebugAction(action))
                return new ActionExecution(new Error(
                    $"Action {actionName} is set as the debug action, but does not contain a Read Debug Data step.\r\n\r\n" +
                    "To configure it, go to Tools -> RAD Debug -> Options and edit your current profile."));

            var activeFile = _codeEditor.GetAbsoluteSourcePath();
            var activeFileLine = _codeEditor.GetCurrentLine();
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            var breakLines = _breakpointTracker.GoToBreakTarget(activeFile, debugBreakTarget);
            var transients = new MacroEvaluatorTransientValues(activeFileLine, activeFile, breakLines, watches);

            try
            {
                _currentlyRunningActionName = action.Name;
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                _project.Options.DebuggerOptions.UpdateLastAppArgs();

                var projectProperties = _project.GetProjectProperties();
                var remoteEnvironment = _project.Options.Profile.General.RunActionsLocally
                    ? null
                    : new AsyncLazy<IReadOnlyDictionary<string, string>>(_channel.GetRemoteEnvironmentAsync, ThreadHelper.JoinableTaskFactory);

                var evaluator = new MacroEvaluator(projectProperties, transients, remoteEnvironment, _project.Options.DebuggerOptions, _project.Options.Profile);

                var generalResult = await _project.Options.Profile.General.EvaluateAsync(evaluator);
                if (!generalResult.TryGetResult(out var general, out var evalError))
                    return new ActionExecution(evalError);
                var evalResult = await action.EvaluateAsync(evaluator, _project.Options.Profile);
                if (!evalResult.TryGetResult(out action, out evalError))
                    return new ActionExecution(evalError);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _projectSources.SaveProjectState();

                var env = new ActionEnvironment(general.LocalWorkDir, general.RemoteWorkDir, transients.Watches);
                var runner = new ActionRunner(_channel, _serviceProvider, env, _project);
                var runResult = await runner.RunAsync(action.Name, action.Steps, _project.Options.Profile.General.ContinueActionExecOnError).ConfigureAwait(false);
                var actionError = await _actionLogger.LogActionWithWarningsAsync(runResult).ConfigureAwait(false);
                return new ActionExecution(actionError, transients, runResult);
            }
            finally
            {
                _currentlyRunningActionName = null;
                await _statusBar.ClearAsync();
            }
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
    }
}
