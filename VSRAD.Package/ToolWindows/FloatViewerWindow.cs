using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("53BC3931-21E8-4453-8139-0D2AF38B751B")]
    public sealed class FloatViewerWindow : BaseToolWindow
    {
        private FloatViewerControl _floatViewerControl;

        public FloatViewerWindow() : base("RAD Float Viewer") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration)
        {
            _floatViewerControl = new FloatViewerControl();
            return _floatViewerControl;
        }

        public void InspectFloat(uint binaryValue, int floatBitSize)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _floatViewerControl.InspectFloat(binaryValue, floatBitSize);
            var windowFrame = (IVsWindowFrame)Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
