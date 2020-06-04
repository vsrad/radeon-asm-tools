using System.Windows;
using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerControl : UserControl
    {
        private readonly IToolWindowIntegration _integration;
        private readonly VisualizerTable _table;

        private BreakState _breakState;

        public VisualizerControl(IToolWindowIntegration integration)
        {
            InitializeComponent();
            _integration = integration;
            headerControl.Setup(integration,
                getGroupCount: (groupSize) => _breakState?.Data.GetGroupCount(groupSize) ?? 0,
                GroupSelectionChanged);
            headerControl.GroupSizeChanged += RefreshDataStyling;
            Application.Current.Deactivated += (sender, e) => WindowFocusLost();

            integration.BreakEntered += BreakEntered;
            integration.AddWatch += AddWatch;
            integration.ProjectOptions.VisualizerOptions.PropertyChanged += VisualizerOptionsChanged;
            integration.ProjectOptions.VisualizerColumnStyling.PropertyChanged += (s, e) => RefreshDataStyling();
            integration.ProjectOptions.DebuggerOptions.PropertyChanged += DebuggerOptionsChanged;
            integration.ProjectOptions.VisualizerAppearance.PropertyChanged += VisualizerOptionsChanged;

            var tableFontAndColor = new FontAndColorProvider();
            tableFontAndColor.FontAndColorInfoChanged += RefreshDataStyling;
            _table = new VisualizerTable(
                _integration.ProjectOptions.VisualizerColumnStyling,
                _integration.ProjectOptions.VisualizerAppearance,
                tableFontAndColor,
                getGroupSize: () => headerControl.GroupSize);
            _table.WatchStateChanged += (newWatchState, invalidatedRows) =>
            {
                _integration.ProjectOptions.DebuggerOptions.Watches.Clear();
                _integration.ProjectOptions.DebuggerOptions.Watches.AddRange(newWatchState);
                if (invalidatedRows != null)
                    foreach (var row in invalidatedRows)
                        SetRowContentsFromBreakState(row);
            };
            _table.ScalingMode = _integration.ProjectOptions.VisualizerAppearance.ScalingMode;
            tableHost.Setup(_table);
            RestoreSavedState();
        }

        public void WindowFocusLost() =>
            _table.HostWindowDeactivated();

        private void RefreshDataStyling() =>
            _table.ApplyDataStyling(_integration.ProjectOptions, headerControl.GroupSize, _breakState?.Data.GetSystem());

        private void GrayOutWatches() =>
            _table.GrayOutColumns(headerControl.GroupSize);

        private void DebuggerOptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Options.DebuggerOptions.Counter))
                GrayOutWatches();
        }

        private void RestoreSavedState()
        {
            RefreshDataStyling();
            _table.Rows.Clear();
            _table.AppendVariableRow(new Watch("System", VariableType.Hex, isAVGPR: false), canBeRemoved: false);
            _table.ShowSystemRow = _integration.ProjectOptions.VisualizerOptions.ShowSystemVariable;
            _table.AlignmentChanged(
                    _integration.ProjectOptions.VisualizerAppearance.NameColumnAlignment,
                    _integration.ProjectOptions.VisualizerAppearance.DataColumnAlignment,
                    _integration.ProjectOptions.VisualizerAppearance.HeadersAlignment
                );
            foreach (var watch in _integration.ProjectOptions.DebuggerOptions.Watches)
                _table.AppendVariableRow(watch);
            _table.PrepareNewWatchRow();
        }

        private void VisualizerOptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.VisualizerOptions.ShowSystemVariable):
                    _table.ShowSystemRow = _integration.ProjectOptions.VisualizerOptions.ShowSystemVariable;
                    break;
                case nameof(Options.VisualizerAppearance.ScalingMode):
                    _table.ScalingMode = _integration.ProjectOptions.VisualizerAppearance.ScalingMode;
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
                    if (_integration.ProjectOptions.VisualizerOptions.CheckMagicNumber)
                        RefreshDataStyling();
                    break;
                case nameof(Options.VisualizerAppearance.NameColumnAlignment):
                case nameof(Options.VisualizerAppearance.DataColumnAlignment):
                case nameof(Options.VisualizerAppearance.HeadersAlignment):
                    _table.AlignmentChanged(
                        _integration.ProjectOptions.VisualizerAppearance.NameColumnAlignment,
                        _integration.ProjectOptions.VisualizerAppearance.DataColumnAlignment,
                        _integration.ProjectOptions.VisualizerAppearance.HeadersAlignment
                    );
                    break;
            }
        }

        private void BreakEntered(BreakState breakState)
        {
            _breakState = breakState;
            if (_breakState != null)
            {
                headerControl.OnDataAvailable();
                _table.ApplyWatchStyling(_breakState.Data.Watches);
            }
            else
            {
                GrayOutWatches();
            }
        }

        private void AddWatch(string watchName)
        {
            _table.RemoveNewWatchRow();
            _table.AppendVariableRow(new Watch(watchName, VariableType.Hex, isAVGPR: false));
            _table.PrepareNewWatchRow();
            _integration.ProjectOptions.DebuggerOptions.Watches.Clear();
            _integration.ProjectOptions.DebuggerOptions.Watches.AddRange(_table.GetCurrentWatchState());
        }

        private void GroupSelectionChanged(uint groupIndex, string coordinates)
        {
            headerControl.OnPendingDataRequest(coordinates);
            VSPackage.TaskFactory.RunAsync(async () =>
            {
                var warning = await _breakState.Data.ChangeGroupWithWarningsAsync(_integration.CommunicationChannel, (int)groupIndex, (int)headerControl.GroupSize);
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                if (warning != null)
                    Errors.ShowWarning(warning);

                foreach (System.Windows.Forms.DataGridViewRow row in _table.Rows)
                    SetRowContentsFromBreakState(row);

                RefreshDataStyling();
                headerControl.OnDataRequestCompleted(_breakState.Data.GetGroupCount(headerControl.GroupSize), _breakState.TotalElapsedMilliseconds, _breakState.ExecElapsedMilliseconds, _breakState.StatusString);
            });
        }

        private void SetRowContentsFromBreakState(System.Windows.Forms.DataGridViewRow row)
        {
            if (_breakState == null)
                return;
            if (row.Index == 0)
            {
                RenderRowData(row, _breakState.Data.GetSystem());
            }
            else
            {
                var watch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
                var watchData = _breakState.Data.GetWatch(watch);
                if (watchData != null)
                    RenderRowData(row, watchData);
                else
                    EraseRowData(row);
            }
        }

        private static void EraseRowData(System.Windows.Forms.DataGridViewRow row)
        {
            for (int i = 0; i < VisualizerTable.DataColumnCount; ++i)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = "";
        }

        private void RenderRowData(System.Windows.Forms.DataGridViewRow row, WatchView data)
        {
            var variableType = VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString());
            for (int i = 0; i < headerControl.GroupSize; i++)
                row.Cells[i + VisualizerTable.DataColumnOffset].Value = DataFormatter.FormatDword(variableType, data[i]);
        }
    }
}
