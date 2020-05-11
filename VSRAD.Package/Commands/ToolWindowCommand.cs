using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.ToolWindowCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class ToolWindowCommand : BaseCommand
    {
        public override Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            switch (commandId)
            {
                case Constants.ToolWindowVisualizerCommandId:
                case Constants.ToolWindowOptionsCommandId:
                case Constants.ToolWindowSliceVisualizerCommandId:
                    return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
                default:
                    return Task.FromResult(CommandStatusResult.Unhandled);
            }
        }

        public async override Task RunAsync(long commandId)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            switch (commandId)
            {
                case Constants.ToolWindowVisualizerCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.VisualizerToolWindow.Frame).Show());
                    break;
                case Constants.ToolWindowOptionsCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.OptionsToolWindow.Frame).Show());
                    break;
                case Constants.ToolWindowSliceVisualizerCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.SliceVisualizerToolWindow.Frame).Show());
                    break;
            }
        }
    }
}