using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VSRAD.Package.DebugVisualizer;

namespace VSRAD.Package.ToolWindows
{
    [Guid("D3DEA94C-D9B7-4450-B4E5-272C928AAB65")]
    public sealed class SliceVisualizerWindow : BaseToolWindow
    {
        private VisualizerControl _visualizerControl;

        public SliceVisualizerWindow() : base("RAD Slice Visualizer") { }

        protected override UIElement CreateToolControl(IToolWindowIntegration integration)
        {
            _visualizerControl = new VisualizerControl(integration);
            return _visualizerControl;
        }

        protected override void OnWindowFocusLost()
        {
            _visualizerControl.WindowFocusLost();
        }
    }
}
