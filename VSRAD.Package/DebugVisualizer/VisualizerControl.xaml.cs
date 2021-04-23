using System.ComponentModel;
using System.Windows.Controls;
using VSRAD.Package.DebugVisualizer.Wavemap;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerControl : UserControl, IDisposableToolWindow
    {
        private readonly VisualizerTable _table;
        private readonly VisualizerContext _context;
        private readonly WavemapImage _wavemap;
        private readonly FontAndColorProvider _fontAndColorProvider;
        private readonly IToolWindowIntegration _integration;

        public VisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetVisualizerContext();
            _context.PropertyChanged += ContextPropertyChanged;
            _context.GroupFetched += GroupFetched;
            _context.GroupFetching += SetupDataFetch;
            DataContext = _context;
            InitializeComponent();

            _wavemap = new WavemapImage(HeaderHost.WavemapImage, _context);
            _wavemap.NavigationRequested += NavigateToWave;
            HeaderHost.WavemapSelector.Setup(_context, _wavemap);

            _integration = integration;
            _integration.AddWatch += AddWatch;
            PropertyChangedEventManager.AddHandler(_context.Options.VisualizerOptions, OptionsChanged, "");
            PropertyChangedEventManager.AddHandler(_context.Options.DebuggerOptions, OptionsChanged, "");
            PropertyChangedEventManager.AddHandler(_context.Options.VisualizerAppearance, OptionsChanged, "");
            PropertyChangedEventManager.AddHandler(_context.Options.VisualizerColumnStyling, VisualizerColumnStylingPropertyChanged, "");

            _fontAndColorProvider = new FontAndColorProvider();
            _fontAndColorProvider.FontAndColorInfoChanged += RefreshDataStyling;
            _table = new VisualizerTable(
                _context.Options,
                _fontAndColorProvider,
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

        void IDisposableToolWindow.DisposeToolWindow()
        {
            ((DockPanel)Content).Children.Clear();
            TableHost.Dispose();
            _fontAndColorProvider.Dispose();

            _integration.AddWatch -= AddWatch;
        }

        private void NavigateToWave(object sender, WavemapImage.NagivationEventArgs e)
        {
            _context.GroupIndex.GoToGroup(e.GroupIdx);
            if (e.WaveIdx is uint waveIdx)
                _table.GoToWave(waveIdx, _context.Options.DebuggerOptions.WaveSize);
        }

        private void SetupDataFetch(object sender, GroupFetchingEventArgs e)
        {
            e.FetchWholeFile |= _context.Options.VisualizerOptions.ShowWavemap;
        }

        public void WindowFocusChanged(bool hasFocus) =>
            _table.HostWindowFocusChanged(hasFocus);

        private void RefreshDataStyling() =>
            _table.ApplyDataStyling(_context.Options, _context.BreakData?.GetSystem());

        private void GrayOutWatches() =>
            _table.GrayOutRows();

        private void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VisualizerContext.WatchesValid):
                    if (!_context.WatchesValid)
                        GrayOutWatches();
                    break;
            }
        }

        private void GroupFetched(object sender, GroupFetchedEventArgs e)
        {
            if (e.Warning != null)
                Errors.ShowWarning(e.Warning);
            if (e.DispatchParameters == null && !_context.Options.VisualizerOptions.ManualMode)
                Errors.ShowWarning(@"Automatic grid size selection is enabled, but dispatch parameters are unavailable for this run.

To enable dispatch parameters extraction:
1. Go to Tools -> RAD Debug -> Options and open profile editor.
2. Select your current debug action and navigate to the Read Debug Data step.
3. Enter the path to the dispatch parameters file.

To switch to manual grid size selection, right-click on the space next to the Group # field and check ""Manual override dispatch"".");

            _table.ApplyWatchStyling();
            RefreshDataStyling();

            _wavemap.View = _context.BreakData.GetWavemapView();

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

        private void VisualizerColumnStylingPropertyChanged(object sender, PropertyChangedEventArgs e) =>
            RefreshDataStyling();

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
                case nameof(Options.DebuggerOptions.GroupSize):
                case nameof(Options.VisualizerOptions.MaskLanes):
                case nameof(Options.VisualizerOptions.CheckMagicNumber):
                case nameof(Options.VisualizerAppearance.LaneGrouping):
                case nameof(Options.VisualizerAppearance.VerticalSplit):
                case nameof(Options.VisualizerAppearance.LaneSeparatorWidth):
                case nameof(Options.VisualizerAppearance.HiddenColumnSeparatorWidth):
                case nameof(Options.VisualizerAppearance.DarkenAlternatingRowsBy):
                    RefreshDataStyling();
                    break;
                case nameof(Options.DebuggerOptions.WaveSize):
                    RefreshDataStyling();
                    _wavemap.View = _context.BreakData?.GetWavemapView();
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
                case nameof(Options.VisualizerAppearance.BinHexSeparator):
                case nameof(Options.VisualizerAppearance.IntUintSeparator):
                case nameof(Options.VisualizerAppearance.BinHexLeadingZeroes):
                    foreach (System.Windows.Forms.DataGridViewRow row in _table.Rows)
                        SetRowContentsFromBreakState(row);
                    break;
            }
        }

        private void AddWatch(string watchName)
        {
            _table.RemoveNewWatchRow();
            _table.AppendVariableRow(new Watch(watchName, VariableType.Int, isAVGPR: false));
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
                RenderRowData(row, _context.Options.DebuggerOptions.GroupSize, _context.BreakData.GetSystem(),
                    _context.Options.VisualizerAppearance.BinHexSeparator, _context.Options.VisualizerAppearance.IntUintSeparator,
                    _context.Options.VisualizerAppearance.BinHexLeadingZeroes);
            }
            else
            {
                var watch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
                var watchData = _context.BreakData.GetWatch(watch);
                if (watchData != null)
                    RenderRowData(row, _context.Options.DebuggerOptions.GroupSize, watchData,
                        _context.Options.VisualizerAppearance.BinHexSeparator, _context.Options.VisualizerAppearance.IntUintSeparator,
                        _context.Options.VisualizerAppearance.BinHexLeadingZeroes);
                else
                    EraseRowData(row, _table.DataColumnCount);
            }
        }

        private static void EraseRowData(System.Windows.Forms.DataGridViewRow row, int columnCount)
        {
            for (int i = 0; i < columnCount; ++i)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = "";
        }

        private static void RenderRowData(System.Windows.Forms.DataGridViewRow row, uint groupSize, WatchView data, uint binHexSeparator, uint intSeparator, bool leadingZeroes)
        {
            var variableType = VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString());
            for (int i = 0; i < groupSize; i++)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(variableType, data[i],
                                                                            binHexSeparator, intSeparator, leadingZeroes);
        }
    }
}
