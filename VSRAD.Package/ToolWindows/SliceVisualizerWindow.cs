using System;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.DebugVisualizer.SliceVisualizer;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("D3DEA94C-D9B7-4450-B4E5-272C928AAB65")]
    public sealed class SliceVisualizerWindow : BaseToolWindow
    {
        public SliceVisualizerWindow() : base("RAD Slice Visualizer") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration) =>
            new SliceVisualizerControl(integration);
    }
}
