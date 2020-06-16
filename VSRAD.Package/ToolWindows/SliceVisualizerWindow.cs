using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.DebugVisualizer.SliceVisualizer;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("D3DEA94C-D9B7-4450-B4E5-272C928AAB65")]
    public sealed class SliceVisualizerWindow : BaseToolWindow
    {
        private SliceVisualizerControl _sliceVisualizerControl;

        public SliceVisualizerWindow() : base("RAD Slice Visualizer") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration)
        {
            _sliceVisualizerControl = new SliceVisualizerControl(integration);
            return _sliceVisualizerControl;
        }
        /*
        protected override void OnWindowFocusLost()
        {
            //_sliceVisualizerControl.WindowFocusLost();
        }
        */
    }
}
