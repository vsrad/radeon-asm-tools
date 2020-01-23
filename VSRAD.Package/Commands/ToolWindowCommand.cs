using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
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
        [Import]
        internal SVsServiceProvider ServiceProvider = null;

        public override Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
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

        public async override Task<bool> RunAsync(long commandId)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (commandId == Constants.ToolWindowFunctionListCommandId)
            {
                try
                {
                    var dte = (DTE)ServiceProvider.GetService(typeof(DTE));
                    Assumes.Present(dte);
                    dte.ExecuteCommand("View.FunctionList");
                }
                catch (COMException)
                {
                    Errors.ShowCritical("Install RadeonAsmSyntax extension to get this functionality.", title: "Function list is not available");
                }
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