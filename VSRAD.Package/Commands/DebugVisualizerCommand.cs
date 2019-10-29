using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.VisualizerCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class DebugVisualizerCommand : IAsyncCommandGroupHandler
    {
        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            switch (commandId)
            {
                case Constants.VisualizerWindowCommandId:
                case Constants.VisualizerOptionsCommandId:
                    return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
                default:
                    return Task.FromResult(CommandStatusResult.Unhandled);
            }
        }

        public async Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsWindowFrame windowFrame;

            if (commandId == Constants.VisualizerWindowCommandId)
                windowFrame = (IVsWindowFrame)VSPackage.VisualizerToolWindow.Frame;
            else if (commandId == Constants.VisualizerOptionsCommandId)
                windowFrame = (IVsWindowFrame)VSPackage.OptionsToolWindow.Frame;
            else
                return false;

            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            return true;
        }
    }
}