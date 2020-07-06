using System.ComponentModel;
using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed partial class SliceVisualizerControl : UserControl, IDisposableToolWindow
    {
        private readonly SliceVisualizerTable _table;
        private readonly SliceVisualizerContext _context;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetSliceVisualizerContext();
            _context.WatchSelected += WatchSelected;
            _context.HeatMapStateChanged += HeatMapStateChanged;
            _context.DivierWidthChanged += () => _table.ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
            DataContext = _context;
            PropertyChangedEventManager.AddHandler(_context.Options.SliceVisualizerOptions, SliceVisualizerOptionChanged, "");
            InitializeComponent();

            var tableFontAndColor = new FontAndColorProvider();
            _table = new SliceVisualizerTable(tableFontAndColor, _context.Options.VisualizerAppearance);
            _table.ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
            TableHost.Setup(_table);
        }

        void IDisposableToolWindow.DisposeToolWindow()
        {
            ((DockPanel)Content).Children.Clear();
            TableHost.Dispose();
        }

        private void SliceVisualizerOptionChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.VisibleColumns):
                case nameof(Options.SliceVisualizerOptions.SubgroupSize):
                    _table.ColumnStyling.Recompute(
                        _context.Options.SliceVisualizerOptions.SubgroupSize,
                        _context.Options.SliceVisualizerOptions.VisibleColumns
                    );
                    break;
                default:
                    break;
            }
        }

        private void WatchSelected(object sender, TypedSliceWatchView watch) =>
            _table.DisplayWatch(watch, _context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns);

        private void HeatMapStateChanged(object sender, bool state) =>
            _table.SetHeatMapMode(state);
    }
}
