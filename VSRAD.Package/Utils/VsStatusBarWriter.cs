using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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

#pragma warning disable VSTHRD010 // Use CheckAccess instead of ThrowIfNotOnUIThread to avoid exceptions in tests
        public void SetText(string text)
        {
            if (ThreadHelper.CheckAccess())
            {
                if (_statusBar == null)
                    _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));

                _statusBar.FreezeOutput(0);
                _statusBar.SetText(text);
                _statusBar.FreezeOutput(1);
            }
        }
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
    }
}
