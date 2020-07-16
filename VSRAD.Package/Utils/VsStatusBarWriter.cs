using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Utils
{
    public sealed class VsStatusBarWriter
    {
        private readonly SVsServiceProvider _serviceProvider;
        private IVsStatusbar _statusBar;

        public VsStatusBarWriter(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task SetTextAsync(string text)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (_statusBar == null)
                _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));

            _statusBar.FreezeOutput(0);
            _statusBar.SetText(text);
            _statusBar.FreezeOutput(1);
        }

        public async Task ClearAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            _statusBar?.FreezeOutput(0);
            _statusBar?.Clear();
        }
    }
}
