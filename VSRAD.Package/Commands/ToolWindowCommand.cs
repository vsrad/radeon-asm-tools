using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.ToolWindowCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class ToolWindowCommand : IAsyncCommandGroupHandler
    {
        [Import]
        internal SVsServiceProvider ServiceProvider = null;

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            switch (commandId)
            {
                case Constants.ToolWindowVisualizerCommandId:
                case Constants.ToolWindowOptionsCommandId:
                case Constants.ToolWindowFunctionListCommandId:
                    return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
                default:
                    return Task.FromResult(CommandStatusResult.Unhandled);
            }
        }

        public async Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (commandId == Constants.ToolWindowFunctionListCommandId)
            {
                DTE dte = (DTE)ServiceProvider.GetService(typeof(DTE));
                dte?.ExecuteCommand("View.FunctionList");
                return true;
            }

            IVsWindowFrame windowFrame;

            if (commandId == Constants.ToolWindowVisualizerCommandId)
                windowFrame = (IVsWindowFrame)VSPackage.VisualizerToolWindow.Frame;
            else if (commandId == Constants.ToolWindowOptionsCommandId)
                windowFrame = (IVsWindowFrame)VSPackage.OptionsToolWindow.Frame;
            else
                return false;

            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            return true;
        }
    }
}