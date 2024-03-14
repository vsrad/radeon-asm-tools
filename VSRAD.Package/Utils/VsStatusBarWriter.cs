using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Utils
{
    public sealed class VsStatusBarWriter
    {
        private readonly SVsServiceProvider _serviceProvider;
        private IVsStatusbar _statusBar;
        private IVsStatusbar StatusBar
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_statusBar == null)
                    _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
                return _statusBar;
            }
        }

        public VsStatusBarWriter(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task SetTextAsync(string text)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StatusBar.FreezeOutput(0);
            StatusBar.SetText(text);
            StatusBar.FreezeOutput(1);
        }

        public async Task SetTextWithProgressAsync(string text, uint stepsComplete, uint stepsTotal)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StatusBar.FreezeOutput(0);
            var progressBarCookie = 0u;
            StatusBar.Progress(ref progressBarCookie, 1, text, stepsComplete, stepsTotal);
        }

        public async Task ClearAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StatusBar.FreezeOutput(0);
            StatusBar.Clear();
            var progressBarCookie = 0u;
            StatusBar.Progress(ref progressBarCookie, 1, "", 0, 0);
        }
    }
}
