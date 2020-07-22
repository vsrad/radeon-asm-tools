using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ActionsMenuCommand : ICommandHandler
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private ProfileOptions SelectedProfile => _project.Options.Profile;

        [ImportingConstructor]
        public ActionsMenuCommand(
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

        public Guid CommandSet => Constants.ActionsMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (GetActionByCommandId(commandId).TryGetResult(out var action, out _))
            {
                var flags = OleCommandText.GetFlags(commandText);
                if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                    OleCommandText.SetText(commandText, action.Name);

                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            }
            else
            {
                // cannot return SUPPORTED for a dynamic item because it indicates there are more
                if (commandId >= Constants.ActionsMenuCommandId)
                    return 0;

                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            }
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            var actionResult = GetActionByCommandId(commandId);
            if (actionResult.TryGetResult(out var action, out var error))
                VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => ExecuteActionAsync(action));
            else
                Errors.ShowWarning(error.Message);
        }

        private Result<ActionProfileOptions> GetActionByCommandId(uint commandId)
        {
            if (SelectedProfile == null)
                return new Error("A profile is required to use RAD Debug actions. To create it, go to Tools -> RAD Debug -> Options.");

            switch (commandId)
            {
                case Constants.ProfileCommandId:
                    return GetActionByName(SelectedProfile.MenuCommands.ProfileAction);
                case Constants.DisassembleCommandId:
                    return GetActionByName(SelectedProfile.MenuCommands.DisassembleAction);
                case Constants.PreprocessCommandId:
                    return GetActionByName(SelectedProfile.MenuCommands.PreprocessAction);
                default:
                    int index = (int)commandId - Constants.ActionsMenuCommandId;
                    if (index >= SelectedProfile.Actions.Count)
                        return new Error("No actions available. To add an action, go to Tools -> RAD Debug -> Options and edit your current profile.");
                    return SelectedProfile.Actions[index];
            }
        }

        private Result<ActionProfileOptions> GetActionByName(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return new Error($"No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                                  "You can find command mappings in the Toolbar section.");

            var action = SelectedProfile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                return new Error($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                                  "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");
            return action;
        }

        private async Task ExecuteActionAsync(ActionProfileOptions action)
        {
            try
            {
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                var evaluator = await _project.GetMacroEvaluatorAsync().ConfigureAwait(false);
                var envResult = await _project.Options.Profile.General.EvaluateActionEnvironmentAsync(evaluator);
                if (!envResult.TryGetResult(out var env, out var evalError))
                {
                    Errors.Show(evalError);
                    return;
                }
                var evalResult = await action.EvaluateAsync(evaluator, _project.Options.Profile);
                if (!evalResult.TryGetResult(out action, out evalError))
                {
                    Errors.Show(evalError);
                    return;
                }

                await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);

                var runner = new ActionRunner(_channel, _serviceProvider, env);
                var result = await runner.RunAsync(action.Name, action.Steps, Enumerable.Empty<BuiltinActionFile>()).ConfigureAwait(false);
                var actionError = await _actionLogger.LogActionWithWarningsAsync(result).ConfigureAwait(false);
                if (actionError is Error runError)
                    Errors.Show(runError);
            }
            finally
            {
                await _statusBar.ClearAsync();
            }
        }
    }
}
