using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using VSRAD.Package.DebugVisualizer.Wavemap;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerControl : UserControl
    {
        public VisualizerTable Table => _table;
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
                foreach (var row in invalidatedRows)
                    SetRowContentsFromBreakState(row);
            };
            _table.BreakpointLocationRequested += (uint threadId, ref string file, ref uint line) =>
            {
                var waveId = threadId / _context.Options.DebuggerOptions.WaveSize;
                if (_context.BreakData != null && waveId < _context.BreakData.WavesPerGroup)
                {
                    var system = _context.BreakData.GetSystemData((int)waveId);
                    if (system[Server.BreakStateData.SystemMagicNumberLane] == _context.Options.VisualizerOptions.MagicNumber || !_context.Options.VisualizerOptions.CheckMagicNumber)
                    {
                        file = _context.BreakState.BreakFile;
                        line = system[Server.BreakStateData.SystemBreakLineLane];
                    }
                }
            };
            _table.BreakpointNavigationRequested += integration.OpenFileInEditor;
            _table.SetScalingMode(_context.Options.VisualizerAppearance.ScalingMode);
            TableHost.Setup(_table);
            RestoreSavedState();
        }

        private void NavigateToWave(object sender, WavemapImage.NagivationEventArgs e)
        {
            _context.GroupIndex.GoToGroup(e.GroupIndex);
            if (e.WaveIndex is uint waveIdx)
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

            foreach (System.Windows.Forms.DataGridViewRow row in _table.Rows)
                SetRowContentsFromBreakState(row);
        }

        private void RestoreSavedState()
        {
            RefreshDataStyling();
            _table.Rows.Clear();
            _table.InsertUserWatchRow(new Watch("System", new VariableType(VariableCategory.Hex, 32)), canBeRemoved: false);
            _table.ShowSystemRow = _context.Options.VisualizerOptions.ShowSystemVariable;
            _table.AlignmentChanged(
                    _context.Options.VisualizerAppearance.NameColumnAlignment,
                    _context.Options.VisualizerAppearance.DataColumnAlignment,
                    _context.Options.VisualizerAppearance.HeadersAlignment
                );
            foreach (var watch in _context.Options.DebuggerOptions.Watches)
                _table.InsertUserWatchRow(watch);
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

        /// <returns>The number of child item rows.</returns>
        private int SetRowContentsFromBreakState(System.Windows.Forms.DataGridViewRow row)
        {
            var nChildRows = 0;
            if (_context.BreakData != null && row.Cells[VisualizerTable.NameColumnIndex] is WatchNameCell nameCell && nameCell.Value != null)
            {
                var watchType = VariableTypeUtils.TypeFromShortName((string)row.HeaderCell.Value);
                var binHexSeparator = _context.Options.VisualizerAppearance.BinHexSeparator;
                var intSeparator = _context.Options.VisualizerAppearance.IntUintSeparator;
                var leadingZeroes = _context.Options.VisualizerAppearance.BinHexLeadingZeroes;

                if (row.Index == 0) // System watch
                {
                    int tid = 0;
                    for (var wave = 0; wave < _context.BreakData.WavesPerGroup; ++wave)
                    {
                        var data = _context.BreakData.GetSystemData(wave);
                        for (var lane = 0; lane < data.Length && tid < _context.BreakData.GroupSize; ++tid, ++lane)
                        {
                            row.Cells[tid + VisualizerTable.DataColumnOffset].Tag = data[lane];
                            row.Cells[tid + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(watchType, data[lane], binHexSeparator, intSeparator, leadingZeroes);
                        }
                    }
                }
                else
                {
                    Server.WatchMeta watchMeta = null;
                    foreach (var r in nameCell.ParentRows.Append(row))
                    {
                        if (watchMeta == null)
                            watchMeta = _context.BreakData.GetWatchMeta((string)r.Cells[VisualizerTable.NameColumnIndex].Value);
                        else
                            watchMeta = watchMeta.ListItems[((WatchNameCell)r.Cells[VisualizerTable.NameColumnIndex]).IndexInList];
                    }
                    for (int wave = 0, tid = 0; wave < _context.BreakData.WavesPerGroup; ++wave)
                    {
                        var instance = _context.BreakData.GetSystemData(wave)[Server.BreakStateData.SystemInstanceIdLane];
                        var (_, DataSlot, ListSize) = watchMeta != null ? watchMeta.Instances.Find(v => v.Instance == instance) : default;
                        if (DataSlot is uint dataSlot)
                        {
                            var data = _context.BreakData.GetWatchData(wave, (int)dataSlot);
                            for (var lane = 0; lane < data.Length && tid < _context.BreakData.GroupSize; ++tid, ++lane)
                            {
                                row.Cells[tid + VisualizerTable.DataColumnOffset].Tag = data[lane];
                                row.Cells[tid + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(watchType, data[lane], binHexSeparator, intSeparator, leadingZeroes);
                            }
                        }
                        else
                        {
                            var label = (ListSize is uint listSize) ? $"[{listSize}]" : "";
                            for (var lane = 0; lane < _context.BreakData.WaveSize && tid < _context.BreakData.GroupSize; ++tid, ++lane)
                            {
                                row.Cells[tid + VisualizerTable.DataColumnOffset].Tag = null;
                                row.Cells[tid + VisualizerTable.DataColumnOffset].Value = label;
                            }
                        }
                    }
                    for (var i = 0; i < (watchMeta?.ListItems?.Count ?? 0); ++i)
                    {
                        var nextRowIndex = row.Index + nChildRows + 1;
                        if (!(nextRowIndex < _table.NewWatchRowIndex
                            && _table.Rows[nextRowIndex].Cells[VisualizerTable.NameColumnIndex] is WatchNameCell nextNameCell
                            && Enumerable.SequenceEqual(nextNameCell.ParentRows, nameCell.ParentRows.Append(row))))
                        {
                            _table.Rows.Insert(nextRowIndex);
                            _table.Rows[nextRowIndex].HeaderCell.Value = VariableTypeUtils.ShortName(watchType); // Inherit watch type
                            ((WatchNameCell)_table.Rows[nextRowIndex].Cells[VisualizerTable.NameColumnIndex]).IndexInList = i;
                            ((WatchNameCell)_table.Rows[nextRowIndex].Cells[VisualizerTable.NameColumnIndex]).ParentRows.AddRange(nameCell.ParentRows.Append(row));
                            ((WatchNameCell)_table.Rows[nextRowIndex].Cells[VisualizerTable.NameColumnIndex]).ExpandCollapse(nameCell.ListExpanded);
                        }
                        nChildRows += 1;
                        nChildRows += SetRowContentsFromBreakState(_table.Rows[nextRowIndex]);
                    }
                    // Remove all child rows that no longer match watch data
                    for (var r = row.Index + nChildRows + 1; r < _table.NewWatchRowIndex && ((WatchNameCell)_table.Rows[r].Cells[VisualizerTable.NameColumnIndex]).ParentRows.Count > nameCell.ParentRows.Count;)
                        _table.Rows.RemoveAt(r);
                }
            }
            return nChildRows;
        }
    }
}
