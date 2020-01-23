using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    public abstract class BaseRemoteCommand : BaseCommand
    {
        protected readonly SVsServiceProvider _serviceProvider;
        protected readonly int _commandId;

        private IVsStatusbar _statusBar;

        protected BaseRemoteCommand(int commandId, SVsServiceProvider serviceProvider)
        {
            _commandId = commandId;
            _serviceProvider = serviceProvider;
        }

        public override Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            var status = commandId == _commandId
                ? new CommandStatusResult(true, commandText, CommandStatus.Supported | CommandStatus.Enabled)
                : CommandStatusResult.Unhandled;
            return Task.FromResult(status);
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

        protected void OpenFileInEditor(string path, string lineMarker = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);
            dte.ItemOperations.OpenFile(path);

            if (string.IsNullOrEmpty(lineMarker)) return;

            var lineNumber = GetMarkedLineNumber(path, lineMarker);

            var textManager = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);

            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            activeView.SetCaretPos(lineNumber, 0);
        }

        private int GetMarkedLineNumber(string file, string lineMarker)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (line == lineMarker)
                    return lineNumber;
                ++lineNumber;
            }
            return 0;
        }
    }
}
