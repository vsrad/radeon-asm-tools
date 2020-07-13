using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
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

        private IReadOnlyList<ActionProfileOptions> Actions =>
            (IReadOnlyList<ActionProfileOptions>)_project.Options.Profile?.Actions ?? new List<ActionProfileOptions>();

        [ImportingConstructor]
        public ActionsMenuCommand(IProject project, IActionLogger actionLogger, ICommunicationChannel channel, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _actionLogger = actionLogger;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _statusBar = new VsStatusBarWriter(serviceProvider);
        }

        public Guid CommandSet => Constants.QuickActionsMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.ProfileCommandId || commandId == Constants.DisassembleCommandId || commandId == Constants.PreprocessCommandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

            int index = (int)commandId - Constants.ActionsMenuCommandId;
            if (index < 0 || index >= Actions.Count)
                return 0;

            var flags = OleCommandText.GetFlags(commandText);
            if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                OleCommandText.SetText(commandText, Actions[index].Name);

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            ActionProfileOptions actionProfileOptions;

            switch (commandId)
            {
                case Constants.PreprocessCommandId:
                    actionProfileOptions = GetQuickActionCommandByName("Preprocess");
                    break;
                case Constants.ProfileCommandId:
                    actionProfileOptions = GetQuickActionCommandByName("Profile");
                    break;
                case Constants.DisassembleCommandId:
                    actionProfileOptions = GetQuickActionCommandByName("Disassemble");
                    break;
                default:
                    int index = (int)commandId - Constants.ActionsMenuCommandId;
                    if (index == 0 && Actions.Count == 0)
                    {
                        Errors.ShowWarning("No actions available. To add an action, go to Tools -> RAD Debug -> Options and edit your current profile.");
                        return;
                    }
                    actionProfileOptions = Actions[index];
                    break;
            }

            if (actionProfileOptions == null) return;

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => ExecuteActionAsync(actionProfileOptions));
        }

        private void ShowActionNotDefinedWarning(string actionName) =>
            Errors.ShowWarning($"Action {actionName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.");

        private ActionProfileOptions GetQuickActionCommandByName(string actionName)
        {
            var res = Actions.FirstOrDefault(a => a.Name == actionName);
            if (res == null)
                ShowActionNotDefinedWarning(actionName);
            return res;
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
                var actionError = await _actionLogger.LogActionWithWarningsAsync(action.Name, result).ConfigureAwait(false);
                if (actionError is Error e1)
                    Errors.Show(e1);
            }
            finally
            {
                await _statusBar.ClearAsync();
            }
        }
    }
}
