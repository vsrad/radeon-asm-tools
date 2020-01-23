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
                case Constants.ToolWindowFunctionListCommandId:
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
                    break;
            }
        }
    }
}