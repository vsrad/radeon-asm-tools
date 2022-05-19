using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ActionsMenuCommand : ICommandHandler
    {
        private readonly IProject _project;
        private readonly IActionLauncher _actionLauncher;

        private ProfileOptions SelectedProfile => _project.Options.Profile;

        [ImportingConstructor]
        public ActionsMenuCommand(IProject project, IActionLauncher actionLauncher)
        {
            _project = project;
            _actionLauncher = actionLauncher;
        }

        public Guid CommandSet => Constants.ActionsMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (GetActionNameByCommandId(commandId).TryGetResult(out var actionName, out _)
                && SelectedProfile.Actions.Any(a => a.Name == actionName))
            {
                var flags = OleCommandText.GetFlags(commandText);
                if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                    OleCommandText.SetText(commandText, actionName);

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
            if (GetActionNameByCommandId(commandId).TryGetResult(out var actionName, out var error))
            {
                var isDebugAction = (SelectedProfile.Actions.FirstOrDefault(a => a.Name == actionName) is ActionProfileOptions action
                        && _actionLauncher.IsDebugAction(action, new System.Collections.Generic.List<string>()));
                if (_actionLauncher.TryLaunchActionByName(actionName, moveToNextDebugTarget: isDebugAction) is Error e)
                    Errors.Show(e);
            }
            else
            {
                Errors.ShowWarning(error.Message);
            }
        }

        private Result<string> GetActionNameByCommandId(uint commandId)
        {
            if (SelectedProfile == null)
                return new Error("A profile is required to use RAD Debug actions. To create it, go to Tools -> RAD Debug -> Options.");

            switch (commandId)
            {
                case Constants.ProfileCommandId:
                    return SelectedProfile.MenuCommands.ProfileAction;
                case Constants.DisassembleCommandId:
                    return SelectedProfile.MenuCommands.DisassembleAction;
                case Constants.PreprocessCommandId:
                    return SelectedProfile.MenuCommands.PreprocessAction;
                case Constants.DebugActionCommandId:
                    return SelectedProfile.MenuCommands.DebugAction;
                default:
                    int index = (int)commandId - Constants.ActionsMenuCommandId;
                    if (index >= SelectedProfile.Actions.Count)
                        return new Error("No actions available. To add an action, go to Tools -> RAD Debug -> Options and edit your current profile.");
                    return SelectedProfile.Actions[index].Name;
            }
        }
    }
}
