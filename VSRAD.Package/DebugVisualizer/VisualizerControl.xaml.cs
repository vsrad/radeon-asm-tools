using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using VSRAD.Package.DebugVisualizer.Wavemap;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerControl : UserControl
    {
        private readonly VisualizerTable _table;
        private readonly VisualizerContext _context;
        private readonly WavemapImage _wavemap;

        public VisualizerControl(IToolWindowIntegration integration, IFontAndColorProvider fontAndColorProvider)
        {
            _context = integration.GetVisualizerContext();
            _context.PropertyChanged += VisualizerStateChanged;
            _context.GroupFetched += GroupFetched;
            _context.GroupFetching += SetupDataFetch;
            DataContext = _context;
            InitializeComponent();

            _wavemap = new WavemapImage(HeaderHost.WavemapImage, _context);
            _wavemap.NavigationRequested += NavigateToWave;
            HeaderHost.WavemapSelector.Setup(_context, _wavemap);

            integration.ProjectOptions.VisualizerOptions.PropertyChanged += VisualizerStateChanged;
            integration.ProjectOptions.VisualizerColumnStyling.PropertyChanged += (s, e) => RefreshDataStyling();
            integration.ProjectOptions.DebuggerOptions.PropertyChanged += VisualizerStateChanged;
            integration.ProjectOptions.VisualizerAppearance.PropertyChanged += VisualizerStateChanged;

            fontAndColorProvider.FontAndColorInfoChanged += RefreshDataStyling;
            _table = new VisualizerTable(_context.Options, fontAndColorProvider);
            integration.AddWatch += _table.AddWatch;
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
            _table.ApplyDataStyling(_context.Options, _context.BreakData);

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

            RefreshDataStyling();

            _wavemap.View = _context.BreakData.GetWavemapView();

            foreach (System.Windows.Forms.DataGridViewRow row in _table.Rows)
                SetRowContentsFromBreakState(row);
        }

        private void RestoreSavedState()
        {
            RefreshDataStyling();
            _table.Rows.Clear();
            _table.AppendVariableRow(new Watch("System", new VariableType(VariableCategory.Hex, 32)), canBeRemoved: false);
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

        private void VisualizerStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VisualizerContext.WatchDataValid):
                    _table.WatchDataValid = _context.WatchDataValid;
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

        private void SetRowContentsFromBreakState(System.Windows.Forms.DataGridViewRow row)
        {
            var watch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
            if (_context.BreakData != null && watch != null)
            {
                var watchType = VariableTypeUtils.TypeFromShortName((string)row.HeaderCell.Value);
                var binHexSeparator = _context.Options.VisualizerAppearance.BinHexSeparator;
                var intSeparator = _context.Options.VisualizerAppearance.IntUintSeparator;
                var leadingZeroes = _context.Options.VisualizerAppearance.BinHexLeadingZeroes;

                foreach (var waveView in _context.BreakData.GetWaveViews())
                {
                    var endThreadId = Math.Min(waveView.StartThreadId + waveView.WaveSize, _table.DataColumnCount);
                    IEnumerable<uint> watchData = row.Index == 0 ? waveView.GetSystem() : waveView.GetWatchOrNull(watch);
                    if (watchData == null)
                    {
                        for (var tid = waveView.StartThreadId; tid < endThreadId; ++tid)
                            row.Cells[tid + VisualizerTable.DataColumnOffset].Value = "";
                    }
                    else
                    {
                        var tid = waveView.StartThreadId;
                        foreach (var value in watchData)
                        {
                            if (tid < endThreadId)
                                row.Cells[(tid++) + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(watchType, value, binHexSeparator, intSeparator, leadingZeroes);
                        }
                    }
                }
            }
        }
    }
}
