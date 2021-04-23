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
        public VisualizerWindow() : base("RAD Debug Visualizer") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration) =>
            new VisualizerControl(integration);

        protected override void OnWindowFocusChanged(bool hasFocus)
        {
            if (Control is VisualizerControl visualizer)
                visualizer.WindowFocusChanged(hasFocus);
        }
    }
}
