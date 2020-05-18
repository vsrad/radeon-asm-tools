using System;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("FFF7ADA4-DCA6-4C7C-A04C-3DA9EAA36953")]
    public sealed class OptionsWindow : BaseToolWindow
    {
        public OptionsWindow() : base("RAD Options") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration) =>
            new OptionsControl(integration);
    }
}
