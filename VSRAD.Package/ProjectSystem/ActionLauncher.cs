using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionLauncher
    {
        Task<ActionRunResult> LaunchActionByNameAsync(string actionName, MacroEvaluatorTransientValues transients);
    }

    [Export(typeof(IActionLauncher))]
    public sealed class ActionLauncher : IActionLauncher
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private string _currentlyRunningActionName;

        [ImportingConstructor]
        public ActionLauncher(
            IProject project,
            IActionLogger actionLogger,
            ICommunicationChannel channel,
            IFileSynchronizationManager deployManager,
            SVsServiceProvider serviceProvider)
        {
            _project = project;
            _actionLogger = actionLogger;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _deployManager = deployManager;
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public async Task<ActionRunResult> LaunchActionByNameAsync(string actionName, MacroEvaluatorTransientValues transients)
        {
            if (_currentlyRunningActionName != null)
            {
                Errors.ShowWarning($"Action {_currentlyRunningActionName} is already running.\r\n\r\n" +
                    "If you believe this to be a hang, use the Disconnect button available in Tools -> RAD Debug -> Options to abort the currently running action.");
                return null;
            }
            if (string.IsNullOrEmpty(actionName))
            {
                Errors.ShowWarning("No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "You can find command mappings in the Toolbar section.");
                return null;
            }
            var action = _project.Options.Profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
            {
                Errors.ShowWarning($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");
                return null;
            }
            try
            {
                _currentlyRunningActionName = action.Name;
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                var evaluator = await _project.GetMacroEvaluatorAsync(transients).ConfigureAwait(false);
                var envResult = await _project.Options.Profile.General.EvaluateActionEnvironmentAsync(evaluator);
                if (!envResult.TryGetResult(out var env, out var evalError))
                {
                    Errors.Show(evalError);
                    return null;
                }
                var evalResult = await action.EvaluateAsync(evaluator, _project.Options.Profile);
                if (!evalResult.TryGetResult(out action, out evalError))
                {
                    Errors.Show(evalError);
                    return null;
                }

                await _deployManager.SynchronizeRemoteAsync(evaluator).ConfigureAwait(false);

                var runner = new ActionRunner(_channel, _serviceProvider, env);
                var result = await runner.RunAsync(action.Name, action.Steps, _project.Options.Profile.General.ContinueActionExecOnError).ConfigureAwait(false);
                var actionError = await _actionLogger.LogActionWithWarningsAsync(result).ConfigureAwait(false);
                if (actionError is Error runError)
                    Errors.Show(runError);

                return result;
            }
            finally
            {
                _currentlyRunningActionName = null;
                await _statusBar.ClearAsync();
            }
        }
    }
}
