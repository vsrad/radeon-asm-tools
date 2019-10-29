using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    public abstract class BaseRemoteCommand : IAsyncCommandGroupHandler
    {
        protected readonly SVsServiceProvider _serviceProvider;
        private readonly int _commandId;

        private IVsStatusbar _statusBar;

        public BaseRemoteCommand(int commandId, SVsServiceProvider serviceProvider)
        {
            _commandId = commandId;
            _serviceProvider = serviceProvider;
        }

        public abstract Task RunAsync();

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            var status = commandId == _commandId
                ? new CommandStatusResult(true, commandText, CommandStatus.Supported | CommandStatus.Enabled)
                : CommandStatusResult.Unhandled;
            return Task.FromResult(status);
        }

        public Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            if (commandId != _commandId) return Task.FromResult(false);

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(RunAsync);

            return Task.FromResult(true);
        }

        protected async Task SetStatusBarTextAsync(string text)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (_statusBar == null)
            {
                _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
            }

            _statusBar.FreezeOutput(0);
            _statusBar.SetText(text);
            _statusBar.FreezeOutput(1);
        }

        protected async Task ClearStatusBarAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            _statusBar?.FreezeOutput(0);
            _statusBar?.Clear();
        }
    }
}
