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
            _context.WatchSelected += WatchSelected;
            _context.HeatMapStateChanged += HeatMapStateChanged;
            DataContext = _context;
            InitializeComponent();

            var tableFontAndColor = new FontAndColorProvider();
            _table = new SliceVisualizerTable(tableFontAndColor);
            TableHost.Setup(_table);
        }

        private void WatchSelected(object sender, TypedSliceWatchView watch) =>
            _table.DisplayWatch(watch);

        private void HeatMapStateChanged(object sender, bool state) =>
            _table.SetHeatMapMode(state);
    }
}
