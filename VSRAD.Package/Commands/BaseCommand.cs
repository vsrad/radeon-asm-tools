using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace VSRAD.Package.Commands
{
    public abstract class BaseCommand : IAsyncCommandGroupHandler
    {
        public abstract Task RunAsync(long commandId);
        public abstract Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus);
        public Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            // Important: don't replace this with await; for some reason, awaiting here locks up the UI thread.
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => RunAsync(commandId));
            return Task.FromResult(true);
        }
    }
}
