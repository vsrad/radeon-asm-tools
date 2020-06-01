using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    public abstract class BaseRemoteCommand : ICommandHandler
    {
        protected readonly SVsServiceProvider _serviceProvider;
        protected readonly int _commandId;

        private IVsStatusbar _statusBar;

        public Guid CommandSet { get; }

        protected BaseRemoteCommand(Guid commandSet, int commandId, SVsServiceProvider serviceProvider)
        {
            CommandSet = commandSet;
            _commandId = commandId;
            _serviceProvider = serviceProvider;
        }

        public OLECMDF GetCommandStatus(uint commandId)
        {
            if (commandId == _commandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            return 0;
        }

        public abstract Task RunAsync();

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut) =>
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(RunAsync);

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

        private static int GetMarkedLineNumber(string file, string lineMarker)
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
