using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class StatusBarIntegration
    {
        private readonly SVsServiceProvider _serviceProvider;
        private IVsStatusbar _statusBar;

        [ImportingConstructor]
        public StatusBarIntegration(SVsServiceProvider serviceProvider, IActionLauncher actionLauncher)
        {
            _serviceProvider = serviceProvider;
            actionLauncher.ActionExecutionStateChanged += ShowActionStateInStatusBar;
        }

        private void ShowActionStateInStatusBar(object sender, ActionExecutionStateChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_statusBar == null)
                _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));

            _statusBar.FreezeOutput(0);
            switch (e.State)
            {
                case ActionExecutionState.Started:
                    _statusBar.SetText("Running " + e.ActionName + " action...");
                    break;
                case ActionExecutionState.Finished:
                    _statusBar.SetText("Finished running " + e.ActionName + " action");
                    break;
                case ActionExecutionState.Cancelling:
                    _statusBar.SetText("Cancelling " + e.ActionName + " action...");
                    break;
            }
            _statusBar.FreezeOutput(1);
        }
    }
}
