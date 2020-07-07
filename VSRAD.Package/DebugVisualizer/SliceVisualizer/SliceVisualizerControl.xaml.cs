using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public partial class SliceVisualizerControl : UserControl
    {
        private readonly SliceVisualizerTable _table;
        private readonly SliceVisualizerContext _context;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetSliceVisualizerContext();
            DataContext = _context;
            InitializeComponent();

            var tableFontAndColor = new FontAndColorProvider();
            _table = new SliceVisualizerTable(_context, tableFontAndColor);
            TableHost.Setup(_table);
        }
    }
}
