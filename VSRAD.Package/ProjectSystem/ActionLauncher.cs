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
    public interface IActionLauncher
    {
        Task<Result<ActionRunResult>> LaunchActionByNameAsync(string actionName, BreakTargetSelector debugBreakTarget = BreakTargetSelector.Last);
        bool IsDebugAction(ActionProfileOptions action);
    }

    [Export(typeof(IActionLauncher))]
    public sealed class ActionLauncher : IActionLauncher
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private string _currentlyRunningActionName;

        [ImportingConstructor]
        public ActionLauncher(
            IProject project,
            ICommunicationChannel channel,
            IProjectSourceManager projectSourceManager,
            IBreakpointTracker breakpointTracker,
            SVsServiceProvider serviceProvider)
        {
            _project = project;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _projectSourceManager = projectSourceManager;
            _breakpointTracker = breakpointTracker;
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public async Task<Result<ActionRunResult>> LaunchActionByNameAsync(string actionName, BreakTargetSelector debugBreakTarget = BreakTargetSelector.Last)
        {
            if (_currentlyRunningActionName != null)
                return new Error($"Action {_currentlyRunningActionName} is already running.\r\n\r\n" +
                    "If you believe this to be a hang, use the Disconnect button available in Tools -> RAD Debug -> Options to abort the currently running action.");

            if (string.IsNullOrEmpty(actionName))
                return new Error("No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "You can find command mappings in the Toolbar section.");

            var action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                return new Error($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");

            if (debugBreakTarget != BreakTargetSelector.Last && !IsDebugAction(action))
                return new Error($"Action {actionName} is set as the debug action, but does not contain a Read Debug Data step.\r\n\r\n" +
                    "To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.");

            var activeEditor = _projectSourceManager.GetActiveEditorView();
            var (activeFile, activeFileLine) = (activeEditor.GetFilePath(), activeEditor.GetCaretPos().Line);
            var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
            var breakTargets = _breakpointTracker.GoToBreakTarget(activeFile, debugBreakTarget);
            var transients = new MacroEvaluatorTransientValues(activeFileLine, activeFile, watches);

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
                    return evalError;
                var evalResult = await action.EvaluateAsync(evaluator, _project.Options.Profile);
                if (!evalResult.TryGetResult(out action, out evalError))
                    return evalError;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _projectSourceManager.SaveProjectState();

                var env = new ActionEnvironment(general.LocalWorkDir, general.RemoteWorkDir, transients.Watches, breakTargets);
                var runner = new ActionRunner(_channel, _serviceProvider, env, _project);
                var runResult = await runner.RunAsync(action.Name, action.Steps, _project.Options.Profile.General.ContinueActionExecOnError).ConfigureAwait(false);
                return runResult;
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
