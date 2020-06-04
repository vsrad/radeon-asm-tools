using System.Windows.Controls;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerHeaderControl : UserControl
    {
        public VisualizerHeaderControl() =>
            InitializeComponent();

        public void Setup(VisualizerContext context) =>
            DataContext = context;
    }
}
