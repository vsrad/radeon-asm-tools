using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("F96E955E-3311-4642-A7E5-DD3D568A2895")]
    public sealed class VisualizerWindow : BaseToolWindow
    {
        private VisualizerControl _visualizerControl;

        public VisualizerWindow() : base(Constants.VisualizerWindowTitle) { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration)
        { 
            _visualizerControl = new VisualizerControl(integration, ActivateWindow);
            return _visualizerControl;
        }

        protected override void OnWindowFocusChanged(bool hasFocus) =>
            _visualizerControl.WindowFocusChanged(hasFocus);

        private void ActivateWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dte.Windows.Item(Constants.VisualizerWindowTitle).Activate();
        }
    }
}
