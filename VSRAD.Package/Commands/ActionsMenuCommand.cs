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
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private ProfileOptions SelectedProfile => _project.Options.Profile;

        [ImportingConstructor]
        public ActionsMenuCommand(IProject project, IActionLogger actionLogger, ICommunicationChannel channel, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _actionLogger = actionLogger;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public Guid CommandSet => Constants.ActionsMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.ProfileCommandId || commandId == Constants.DisassembleCommandId || commandId == Constants.PreprocessCommandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

            int index = (int)commandId - Constants.ActionsMenuCommandId;
            if (SelectedProfile == null || index < 0 || index >= SelectedProfile.Actions.Count)
                return 0;

            var flags = OleCommandText.GetFlags(commandText);
            if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                OleCommandText.SetText(commandText, SelectedProfile.Actions[index].Name);

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (SelectedProfile == null)
            {
                Errors.ShowWarning("A profile is required to run actions. To create it, go to Tools -> RAD Debug -> Options.");
                return;
            }

            var action = GetActionByCommandId(commandId);
            if (action != null)
                VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => ExecuteActionAsync(action));
        }

        private ActionProfileOptions GetActionByCommandId(uint commandId)
        {
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
                    if (index == 0 && SelectedProfile.Actions.Count == 0)
                    {
                        Errors.ShowWarning("No actions available. To add an action, go to Tools -> RAD Debug -> Options and edit your current profile.");
                        return null;
                    }
                    return SelectedProfile.Actions[index];
            }
        }

        private ActionProfileOptions GetActionByName(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                Errors.ShowWarning($"No action is set for this command. To configure it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                                    "You can find command mappings in the Toolbar section.");
                return null;
            }

            var action = SelectedProfile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null)
                Errors.ShowWarning($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.\r\n\r\n" +
                                    "Alternatively, you can set a different action for this command in the Toolbar section of your profile.");
            return action;
        }

        private async Task ExecuteActionAsync(ActionProfileOptions action)
        {
            try
            {
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                var evaluator = await _project.GetMacroEvaluatorAsync().ConfigureAwait(false);
                var env = await _project.Options.Profile.General.EvaluateActionEnvironmentAsync(evaluator);
                action = await action.EvaluateAsync(evaluator, _project.Options.Profile);

                var runner = new ActionRunner(_channel, _serviceProvider, env);
                var result = await runner.RunAsync(action.Name, action.Steps, Enumerable.Empty<BuiltinActionFile>()).ConfigureAwait(false);
                var actionError = await _actionLogger.LogActionWithWarningsAsync(result).ConfigureAwait(false);
                if (actionError is Error e)
                    Errors.Show(e);
            }
            finally
            {
                await _statusBar.ClearAsync();
            }
        }
    }
}
