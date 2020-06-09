using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public partial class SliceVisualizerControl : UserControl
    {
        private readonly SliceVisualizerTable _table;
        private readonly VisualizerContext _context;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetVisualizerContext();
            _context.GroupFetched += GroupFetched;

            var tableFontAndColor = new FontAndColorProvider();
            InitializeComponent();
            _table = new SliceVisualizerTable(tableFontAndColor);
            headerControl.Setup(integration, WatchSelected, ToggleHeatMap);
            tableHost.Setup(_table);
        }

        private void GroupFetched(object sender, GroupFetchedEventArgs e)
        {
            if (!_context.SliceContext.WindowVisibile)
                return;
            var selectedWatch = headerControl.GetSelectedWatch();
            if (selectedWatch != null)
                WatchSelected(selectedWatch);
        }

        private void WatchSelected(string watchName)
        {
            if (_context.BreakData == null)
                return;
            var watch = _context.BreakData.GetSliceWatch(watchName, headerControl.GroupsInRow());
            _table.DisplayWatch(watch);
        }

        private void ToggleHeatMap(bool heatMapActive)
        {
            // TODO: options
        }
    }
}
