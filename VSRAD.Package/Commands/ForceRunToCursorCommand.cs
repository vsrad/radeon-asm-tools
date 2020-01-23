using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.ForceRunToCursorCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class ForceRunToCursorCommand : BaseCommand
    {
        private readonly ProjectSystem.DebuggerIntegration _debugger;

        [ImportingConstructor]
        public ForceRunToCursorCommand(ProjectSystem.DebuggerIntegration debugger)
        {
            _debugger = debugger;
        }

        public override Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            if (commandId == Constants.MenuCommandId)
                return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
            return Task.FromResult(CommandStatusResult.Unhandled);
        }

        public async override Task RunAsync(long commandId)
        {
            if (commandId == Constants.MenuCommandId)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _debugger.RunToCurrentLine();
            }
        }
    }
}