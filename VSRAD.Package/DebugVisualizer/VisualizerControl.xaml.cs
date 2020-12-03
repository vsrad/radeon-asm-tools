using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerControl : UserControl
    {
        private readonly VisualizerTable _table;
        private readonly VisualizerContext _context;
        private readonly WavemapCanvas _wavemap;

        public VisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetVisualizerContext();
            _context.PropertyChanged += ContextPropertyChanged;
            _context.Options.DebuggerOptions.PropertyChanged += DebuggerOptionChanged;
            _context.Options.VisualizerOptions.PropertyChanged += HandleWavemapElementSize;
            _context.GroupFetched += GroupFetched;
            _context.GroupFetching += SetupDataFetch;
            DataContext = _context;
            InitializeComponent();

            _wavemap = new WavemapCanvas(HeaderHost.WavemapCanvas, _context.Options.VisualizerOptions.WavemapElementSize);

            integration.AddWatch += AddWatch;
            integration.ProjectOptions.VisualizerOptions.PropertyChanged += OptionsChanged;
            integration.ProjectOptions.VisualizerColumnStyling.PropertyChanged += (s, e) => RefreshDataStyling();
            integration.ProjectOptions.DebuggerOptions.PropertyChanged += OptionsChanged;
            integration.ProjectOptions.VisualizerAppearance.PropertyChanged += OptionsChanged;

            var tableFontAndColor = new FontAndColorProvider();
            tableFontAndColor.FontAndColorInfoChanged += RefreshDataStyling;
            _table = new VisualizerTable(
                _context.Options,
                tableFontAndColor,
                getValidWatches: () => _context?.BreakData?.Watches);
            _table.WatchStateChanged += (newWatchState, invalidatedRows) =>
            {
                _context.Options.DebuggerOptions.Watches.Clear();
                _context.Options.DebuggerOptions.Watches.AddRange(newWatchState);
                if (invalidatedRows != null)
                    foreach (var row in invalidatedRows)
                        SetRowContentsFromBreakState(row);
            };
            _table.SetScalingMode(_context.Options.VisualizerAppearance.ScalingMode);
            TableHost.Setup(_table);
            RestoreSavedState();
        }

        private void HandleWavemapElementSize(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerOptions.WavemapElementSize))
            {
                _wavemap.RectangleSize = _context.Options.VisualizerOptions.WavemapElementSize;
                _context.CanvasWidth = _wavemap.Width;
                _context.CanvasHeight = _wavemap.Height;
            }
        }

        private void SetupDataFetch(object sender, GroupFetchingEventArgs e)
        {
            e.FetchWholeFile |= _context.Options.VisualizerOptions.ShowWavemapField;
        }

        private void DebuggerOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerContext.Options.DebuggerOptions.GroupSize))
                RefreshDataStyling();
        }

        public void WindowFocusChanged(bool hasFocus) =>
            _table.HostWindowFocusChanged(hasFocus);

        private void RefreshDataStyling() =>
            _table.ApplyDataStyling(_context.Options, _context.Options.DebuggerOptions.GroupSize, _context.BreakData?.GetSystem());

        private void GrayOutWatches() =>
            _table.GrayOutRows();

        private void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerContext.WatchesValid))
            {
                if (_context.WatchesValid)
                    RefreshDataStyling();
                else
                    GrayOutWatches();
            }
        }

        private void GroupFetched(object sender, GroupFetchedEventArgs e)
        {
            if (e.Warning != null)
                Errors.ShowWarning(e.Warning);

            _table.ApplyWatchStyling();
            RefreshDataStyling();

            _wavemap.SetData(_context.BreakData.GetWavemapView((int)_context.Options.VisualizerOptions.WaveSize));
            //_context.CanvasWidth = _wavemap.Width;
            //_context.CanvasHeight = _wavemap.Height;

            foreach (System.Windows.Forms.DataGridViewRow row in _table.Rows)
                SetRowContentsFromBreakState(row);
        }

        private void RestoreSavedState()
        {
            RefreshDataStyling();
            _table.Rows.Clear();
            _table.AppendVariableRow(new Watch("System", VariableType.Hex, isAVGPR: false), canBeRemoved: false);
            _table.ShowSystemRow = _context.Options.VisualizerOptions.ShowSystemVariable;
            _table.AlignmentChanged(
                    _context.Options.VisualizerAppearance.NameColumnAlignment,
                    _context.Options.VisualizerAppearance.DataColumnAlignment,
                    _context.Options.VisualizerAppearance.HeadersAlignment
                );
            foreach (var watch in _context.Options.DebuggerOptions.Watches)
                _table.AppendVariableRow(watch);
            _table.PrepareNewWatchRow();
        }

        private void OptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.DebuggerOptions.Counter):
                    GrayOutWatches();
                    break;
                case nameof(Options.VisualizerOptions.ShowSystemVariable):
                    _table.ShowSystemRow = _context.Options.VisualizerOptions.ShowSystemVariable;
                    break;
                case nameof(Options.VisualizerAppearance.ScalingMode):
                    _table.SetScalingMode(_context.Options.VisualizerAppearance.ScalingMode);
                    break;
                case nameof(Options.VisualizerOptions.MaskLanes):
                case nameof(Options.VisualizerOptions.LaneGrouping):
                case nameof(Options.VisualizerOptions.CheckMagicNumber):
                case nameof(Options.VisualizerOptions.VerticalSplit):
                case nameof(Options.VisualizerAppearance.LaneSeparatorWidth):
                case nameof(Options.VisualizerAppearance.HiddenColumnSeparatorWidth):
                case nameof(Options.VisualizerAppearance.DarkenAlternatingRowsBy):
                    RefreshDataStyling();
                    break;
                case nameof(Options.VisualizerOptions.MagicNumber):
                    if (_context.Options.VisualizerOptions.CheckMagicNumber)
                        RefreshDataStyling();
                    break;
                case nameof(Options.VisualizerAppearance.NameColumnAlignment):
                case nameof(Options.VisualizerAppearance.DataColumnAlignment):
                case nameof(Options.VisualizerAppearance.HeadersAlignment):
                    _table.AlignmentChanged(
                        _context.Options.VisualizerAppearance.NameColumnAlignment,
                        _context.Options.VisualizerAppearance.DataColumnAlignment,
                        _context.Options.VisualizerAppearance.HeadersAlignment
                    );
                    break;
            }
        }

        private void AddWatch(string watchName)
        {
            _table.RemoveNewWatchRow();
            _table.AppendVariableRow(new Watch(watchName, VariableType.Hex, isAVGPR: false));
            _table.PrepareNewWatchRow();
            _context.Options.DebuggerOptions.Watches.Clear();
            _context.Options.DebuggerOptions.Watches.AddRange(_table.GetCurrentWatchState());
        }

        private void SetRowContentsFromBreakState(System.Windows.Forms.DataGridViewRow row)
        {
            if (_context.BreakData == null)
                return;
            if (row.Index == 0)
            {
                RenderRowData(row, _context.Options.DebuggerOptions.GroupSize, _context.BreakData.GetSystem());
            }
            else
            {
                var watch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
                var watchData = _context.BreakData.GetWatch(watch);
                if (watchData != null)
                    RenderRowData(row, _context.Options.DebuggerOptions.GroupSize, watchData);
                else
                    EraseRowData(row, _table.DataColumnCount);
            }
        }

        private static void EraseRowData(System.Windows.Forms.DataGridViewRow row, int columnCount)
        {
            for (int i = 0; i < columnCount; ++i)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = "";
        }

        private static void RenderRowData(System.Windows.Forms.DataGridViewRow row, uint groupSize, WatchView data)
        {
            var variableType = VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString());
            for (int i = 0; i < groupSize; i++)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(variableType, data[i]);
        }
    }
}
