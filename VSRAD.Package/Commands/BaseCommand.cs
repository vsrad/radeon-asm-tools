using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace VSRAD.Package.Commands
{
    public abstract class BaseCommand : IAsyncCommandGroupHandler
    {
        public abstract Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus);
        public abstract Task<bool> RunAsync(long commandId);
        public Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut) =>
            Errors.HandleErrorAsync(() => RunAsync(commandId), returnOnError: false);
    }
}
