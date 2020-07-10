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
    public sealed class QuickActionCommand : ICommandHandler
    {
        private readonly IProject _project;
        private readonly IActionLogger _actionLogger;
        private readonly ICommunicationChannel _channel;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly VsStatusBarWriter _statusBar;

        private IReadOnlyList<ActionProfileOptions> Actions => _project.Options.Profile.Actions;

        [ImportingConstructor]
        public QuickActionCommand(IProject project, IActionLogger actionLogger, ICommunicationChannel channel, SVsServiceProvider serviceProvider)
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
            return 0;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            string commandName;
            switch (commandId)
            {
                case Constants.PreprocessCommandId:
                    commandName = "Preprocess";
                    break;
                case Constants.ProfileCommandId:
                    commandName = "Profile";
                    break;
                case Constants.DisassembleCommandId:
                    commandName = "Disassemble";
                    break;
                default:
                    commandName = string.Empty;
                    break;
            }

            var action = Actions.FirstOrDefault(a => a.Name == commandName);

            if (action == null)
            {
                Errors.ShowWarning($"Action {commandName} is not defined. To create it, go to Tools -> RAD Debug -> Options and edit your current profile.");
                return;
            }

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => ExecuteActionAsync(action));
        }

        private async Task ExecuteActionAsync(ActionProfileOptions action)
        {
            try
            {
                await _statusBar.SetTextAsync("Running " + action.Name + " action...");

                var runner = new ActionRunner(_channel, _serviceProvider);
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
