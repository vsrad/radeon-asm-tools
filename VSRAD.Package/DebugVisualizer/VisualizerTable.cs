using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class VisualizerTable : DataGridView
    {
        public delegate void ChangeWatchState(List<Watch> newState, IEnumerable<DataGridViewRow> invalidatedRows);
        public delegate uint GetGroupSize();
        public delegate ReadOnlyCollection<string> GetValidWatches();

        private readonly GetValidWatches _getValidWatches;

        public event ChangeWatchState WatchStateChanged;

        public const int NameColumnIndex = 0;
        public const int PhantomColumnIndex = 1;
        public const int DataColumnOffset = 2; // watch name column + phantom column

        public const int SystemRowIndex = 0;
        public int NewWatchRowIndex => RowCount - 1; /* new watches are always entered in the last row */
        public int ReservedColumnsOffset => RowHeadersWidth + Columns[NameColumnIndex].Width;
        public int DataColumnCount { get; private set; } = 512;

        public bool ShowSystemRow
        {
            get => Rows.Count > 0 && Rows[0].Visible;
            set { if (Rows.Count > 0) Rows[0].Visible = value; }
        }

        public IEnumerable<DataGridViewRow> DataRows => Rows
            .Cast<DataGridViewRow>()
            .Where(x => x.Index > 0 && x.Index != NewWatchRowIndex);

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;

        private readonly FontAndColorProvider _fontAndColor;
        private readonly ComputedColumnStyling _computedStyling;

        private string _editedWatchName;

        private readonly TableState _state;

        public VisualizerTable(ProjectOptions options, FontAndColorProvider fontAndColor, GetValidWatches getValidWatches, IToolWindowIntegration toolWindowIntegration) : base()
        {
            _fontAndColor = fontAndColor;
            _computedStyling = new ComputedColumnStyling();
            _getValidWatches = getValidWatches;

            RowHeadersWidth = 30;
            RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            RowHeadersVisible = true;
            AllowUserToResizeColumns = false;
            EditMode = DataGridViewEditMode.EditProgrammatically;
            AllowUserToAddRows = false;
            AllowDrop = true;
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;

            CellEndEdit += WatchEndEdit;
            CellBeginEdit += UpdateEditedWatchName;
            CellDoubleClick += (sender, args) => { if (args.ColumnIndex != -1) BeginEdit(false); };
            CellClick += (sender, args) => { if (args.RowIndex == NewWatchRowIndex) BeginEdit(false); };

            // Custom pan/scale cursors
            GiveFeedback += (sender, e) => e.UseDefaultCursors = false;

            ShowCellToolTips = false;
            DoubleBuffered = true;
            AllowUserToResizeRows = false;
            EnableHeadersVisualStyles = false; // custom font and color settings for cell headers

            _state = new TableState(this, columnWidth: 60);
            SetupColumns();
            Debug.Assert(_state.DataColumnOffset == DataColumnOffset);
            Debug.Assert(_state.PhantomColumnIndex == PhantomColumnIndex);

            _ = new ContextMenus.ContextMenuController(this, new ContextMenus.IContextMenu[]
            {
                new ContextMenus.TypeContextMenu(this, VariableTypeChanged, AvgprStateChanged, ProcessCopy, InsertSeparatorRow, toolWindowIntegration),
                new ContextMenus.CopyContextMenu(this, ProcessCopy),
                new ContextMenus.SubgroupContextMenu(this, _state, options.VisualizerColumnStyling, () => options.DebuggerOptions.GroupSize)
            });
            _ = new CellStyling(this, options.VisualizerAppearance, _computedStyling, _fontAndColor);
            _ = new CustomTableGraphics(this);

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
        }

        public void GoToWave(uint waveIdx, uint waveSize)
        {
            var firstCol = waveIdx * waveSize;
            var lastCol = (waveIdx + 1) * waveSize - 1;
            if (!_selectionController.SelectAllColumnsInRange((int)firstCol + DataColumnOffset, (int)lastCol + DataColumnOffset))
                Errors.ShowWarning($"All columns of the target wave ({firstCol}-{lastCol}) are hidden.");
        }

        public void SetScalingMode(ScalingMode mode) => _state.ScalingMode = mode;

        public void ScaleControls(float scaleFactor)
        {
            var rowHeight = (int)(scaleFactor * 20);
            if (rowHeight != RowTemplate.Height)
            {
                RowTemplate.Height = rowHeight;
                foreach (DataGridViewRow row in Rows)
                    row.Height = rowHeight;
            }
            var headerHeight = (int)(scaleFactor * 24);
            ColumnHeadersHeight = headerHeight;
        }

        public IEnumerable<int> GetSelectedDataColumnIndexes(int includeIndex) =>
            SelectedColumns
                .Cast<DataGridViewColumn>()
                .Select(x => x.Index - _state.DataColumnOffset)
                .Where(x => x >= 0)
                .Append(includeIndex - _state.DataColumnOffset);

        private void ProcessCopy()
        {
            ProcessInsertKey(Keys.Control | Keys.C);
        }

        private void VariableTypeChanged(int rowIndex, VariableType type)
        {
            var changedRows = _selectionController.GetClickTargetRows(rowIndex);
            foreach (var row in changedRows)
                row.HeaderCell.Value = type.ShortName();
            RaiseWatchStateChanged(changedRows);
        }

        private void AvgprStateChanged(int rowIndex, bool newAvgprState)
        {
            var selectedNames = _selectionController.GetClickTargetRows(rowIndex).Select(r => (string)r.Cells[NameColumnIndex].Value);
            var selectedRows = DataRows.Where(r => selectedNames.Contains(r.Cells[NameColumnIndex].Value.ToString()));
            foreach (var row in selectedRows)
                row.HeaderCell.Tag = newAvgprState;
            RaiseWatchStateChanged(selectedRows);
            Invalidate(); // redraw custom avgpr graphics
        }

        public void HostWindowFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            }
            else
            {
                DefaultCellStyle.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
                CancelEdit();
            }
        }

        public List<Watch> GetCurrentWatchState() =>
            DataRows.Select(GetRowWatchState).ToList();

        public static Watch GetRowWatchState(DataGridViewRow row) => new Watch(
            name: row.Cells[NameColumnIndex].Value?.ToString(),
            type: VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString()),
            isAVGPR: (bool)row.HeaderCell.Tag);

        public bool IsAVGPR(string watchName) => DataRows
            .Any(r => r.Cells[NameColumnIndex].Value?.ToString() == watchName && (bool)r.HeaderCell.Tag);

        private void RaiseWatchStateChanged(IEnumerable<DataGridViewRow> invalidatedRows = null) =>
            WatchStateChanged(GetCurrentWatchState(), invalidatedRows);

        private void UpdateEditedWatchName(object sender, EventArgs e)
        {
            if (CurrentCell?.ColumnIndex == NameColumnIndex && CurrentCell.RowIndex != NewRowIndex && CurrentCell.Value != null)
                _editedWatchName = CurrentCell.Value.ToString();
        }

        private void InsertSeparatorRow(int rowIndex, bool after)
        {
            var index = after ? rowIndex + 1 : rowIndex;
            Rows.Insert(index);
            Rows[index].Cells[NameColumnIndex].Value = " ";
            Rows[index].HeaderCell.Value = VariableType.Hex.ToString();
            Rows[index].HeaderCell.Tag = false; // avgpr
            RaiseWatchStateChanged(new[] { Rows[index] });
        }

        public void AppendVariableRow(Watch watch, bool canBeRemoved = true)
        {
            var index = Rows.Add();
            Rows[index].Cells[NameColumnIndex].Value = watch.Name;
            Rows[index].HeaderCell.Value = watch.Type.ShortName();
            Rows[index].HeaderCell.Tag = watch.IsAVGPR;
            Rows[index].DefaultCellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)DataHighlightColor.Inactive];
            LockWatchRowForEditing(Rows[index], canBeRemoved);
        }

        public void RemoveNewWatchRow()
        {
            Rows.Remove(Rows[NewWatchRowIndex]);
        }

        public void PrepareNewWatchRow()
        {
            var newRowIndex = Rows.Add();
            Rows[newRowIndex].Cells[NameColumnIndex].ReadOnly = false;
            Rows[newRowIndex].HeaderCell.Value = "";
            Rows[newRowIndex].HeaderCell.Tag = false; // avgpr
            ClearSelection();
        }

        // Make sure ApplyDataStyling is called after creating columns to set column visibility
        public void CreateMissingDataColumns(int groupSize)
        {
            var columnsMissing = groupSize - (Columns.Count - DataColumnOffset);
            if (columnsMissing > 0)
            {
                var missingColumnStartAt = _state.DataColumns.Count;
                var columns = new DataGridViewColumn[columnsMissing];
                for (int i = 0; i < columnsMissing; ++i)
                {
                    columns[i] = new DataGridViewTextBoxColumn
                    {
                        FillWeight = 1,
                        ReadOnly = true,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = _state.ColumnWidth,
                        HeaderText = (missingColumnStartAt + i).ToString()
                    };
                }
                _state.AddDataColumns(columns);
                Debug.Assert(_state.DataColumnOffset == DataColumnOffset);
                DataColumnCount = _state.DataColumns.Count;
            }
        }

        private void SetupColumns()
        {
            Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                ReadOnly = false,
                Frozen = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            CreateMissingDataColumns(DataColumnCount);
        }

        private void WatchEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = Rows[e.RowIndex];
            var rowWatchName = (string)row.Cells[NameColumnIndex].Value;

            if (e.RowIndex == NewWatchRowIndex)
            {
                if (e.ColumnIndex == NameColumnIndex && !string.IsNullOrEmpty(rowWatchName))
                {
                    var scrollingOffset = HorizontalScrollingOffset;
                    if (!string.IsNullOrWhiteSpace(rowWatchName))
                        Rows[e.RowIndex].Cells[NameColumnIndex].Value = rowWatchName.Trim();
                    Rows[e.RowIndex].HeaderCell.Value = VariableType.Hex.ShortName();
                    Rows[e.RowIndex].HeaderCell.Tag = IsAVGPR(rowWatchName); // avgpr
                    RowStyling.UpdateRowHighlight(row, _fontAndColor.FontAndColorState, _getValidWatches());
                    LockWatchRowForEditing(row);
                    PrepareNewWatchRow();
                    RaiseWatchStateChanged(new[] { row });
                    if (!string.IsNullOrWhiteSpace(rowWatchName))
                    {
                        // We want to focus on the new row, but changing the current cell inside CellEndEdit
                        // may cause us to enter infinite loop unless we do it in an asynchronous delegate
                        BeginInvoke(new MethodInvoker(() =>
                        {
                            CurrentCell = Rows[NewWatchRowIndex].Cells[NameColumnIndex];
                            HorizontalScrollingOffset = scrollingOffset;
                            BeginEdit(true);
                        }));
                    }
                }
            }
            else if (e.RowIndex != 0)
            {
                if (string.IsNullOrEmpty(rowWatchName))
                {
                    // ditto as above (asynchronous delegate to prevent an inifinite loop)
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        Rows.RemoveAt(e.RowIndex);
                        RaiseWatchStateChanged();
                    }));
                }
                else if (rowWatchName != _editedWatchName)
                {
                    Rows[e.RowIndex].HeaderCell.Tag = IsAVGPR(rowWatchName); // avgpr
                    RaiseWatchStateChanged(new[] { row });
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        CurrentCell = Rows[e.RowIndex + 1].Cells[NameColumnIndex];
                        BeginEdit(true);
                    }));
                }
            }
        }

        private static void LockWatchRowForEditing(DataGridViewRow row, bool canBeRemoved = true)
        {
            row.Cells[NameColumnIndex].ReadOnly = !canBeRemoved;
        }

        #region Styling

        public void GrayOutRows()
        {
            if (ShowSystemRow)
                RowStyling.GrayOutRow(_fontAndColor.FontAndColorState, Rows[0]);

            foreach (DataGridViewRow row in DataRows)
            {
                RowStyling.GrayOutRow(_fontAndColor.FontAndColorState, row);
            }

            Invalidate();
        }

        public void ApplyWatchStyling()
        {
            foreach (DataGridViewRow row in Rows)
                RowStyling.UpdateRowHighlight(row, _fontAndColor.FontAndColorState, _getValidWatches());
        }

        public void ApplyRowHighlight(int rowIndex, DataHighlightColor? changeFg = null, DataHighlightColor? changeBg = null)
        {
            foreach (var row in _selectionController.GetClickTargetRows(rowIndex))
                RowStyling.UpdateRowHighlight(row, _fontAndColor.FontAndColorState, _getValidWatches(), changeFg, changeBg);
        }

        private bool _disableColumnWidthChangeHandler = false;

        protected override void OnColumnDividerWidthChanged(DataGridViewColumnEventArgs e)
        {
            /* The base method invokes OnColumnGlobalAutoSize, which is very expensive
             * when changing DividerWidth in bulk, as it is done in ColumnStyling.
             * To prevent slowdowns, we disable the handler before invoking ColumnStyling.Apply */
            if (!_disableColumnWidthChangeHandler)
                base.OnColumnDividerWidthChanged(e);
        }

        public void ApplyDataStyling(ProjectOptions options, Server.WatchView system)
        {
            ((Control)this).SuspendDrawing();
            _disableColumnWidthChangeHandler = true;

            CreateMissingDataColumns((int)options.DebuggerOptions.GroupSize);

            _computedStyling.Recompute(options.VisualizerOptions, options.VisualizerAppearance, options.VisualizerColumnStyling,
                options.DebuggerOptions.GroupSize, options.DebuggerOptions.WaveSize, system);

            ApplyFontAndColorInfo();

            var columnStyling = new ColumnStyling(
                options.VisualizerAppearance,
                options.VisualizerColumnStyling,
                _computedStyling,
                _fontAndColor.FontAndColorState);
            columnStyling.Apply(_state.DataColumns);

            foreach (DataGridViewRow row in Rows)
                if (row.Index != NewWatchRowIndex)
                    RowStyling.UpdateRowHighlight(row, _fontAndColor.FontAndColorState, _getValidWatches());

            _disableColumnWidthChangeHandler = false;
            ((Control)this).ResumeDrawing();
        }

        private void ApplyFontAndColorInfo()
        {
            var state = _fontAndColor.FontAndColorState;

            ColumnHeadersDefaultCellStyle.Font = state.HeaderBold ? state.BoldFont : state.RegularFont;
            ColumnHeadersDefaultCellStyle.ForeColor = state.HeaderForeground;

            RowHeadersDefaultCellStyle.Font = state.WatchNameBold ? state.BoldFont : state.RegularFont;
            RowHeadersDefaultCellStyle.ForeColor = state.WatchNameForeground;
            Columns[0].DefaultCellStyle.Font = state.WatchNameBold ? state.BoldFont : state.RegularFont;
            Columns[0].DefaultCellStyle.ForeColor = state.WatchNameForeground;

            // Disable selection styles because DataGridView does not preserve selected headers when switching selection mode
            ColumnHeadersDefaultCellStyle.SelectionForeColor = state.HeaderForeground;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = ColumnHeadersDefaultCellStyle.BackColor;
            RowHeadersDefaultCellStyle.SelectionForeColor = state.WatchNameForeground;
            RowHeadersDefaultCellStyle.SelectionBackColor = RowHeadersDefaultCellStyle.BackColor;

            DefaultCellStyle.Font = state.RegularFont;
        }

        public void AlignmentChanged(
                ContentAlignment nameColumnAlignment,
                ContentAlignment dataColumnAlignment,
                ContentAlignment headersAlignment)
        {
            Columns[0].DefaultCellStyle.Alignment = nameColumnAlignment.AsDataGridViewContentAlignment();
            Columns[0].HeaderCell.Style.Alignment = headersAlignment.AsDataGridViewContentAlignment();
            foreach (var column in _state.DataColumns)
            {
                column.DefaultCellStyle.Alignment = dataColumnAlignment.AsDataGridViewContentAlignment();
                column.HeaderCell.Style.Alignment = headersAlignment.AsDataGridViewContentAlignment();
            }
        }

        #endregion

        #region Custom CmdKeys handlers

        private bool HandleDelete()
        {
            if (!IsCurrentCellInEditMode)
            {
                var selectedRowsIndexes = _selectionController.GetSelectedRows().Select(r => r.Index).Reverse();
                foreach (var rowIndex in selectedRowsIndexes)
                    if (rowIndex != 0) // deleting System is forbidden
                        Rows.RemoveAt(rowIndex);

                RaiseWatchStateChanged();

                return true;
            }

            return false;
        }

        private bool HandleEnter()
        {
            if (CurrentCell?.ColumnIndex == NameColumnIndex)
            {
                if (!IsCurrentCellInEditMode)
                {
                    BeginEdit(false);
                    return true;
                }
                if (!string.IsNullOrEmpty(CurrentCell?.Value?.ToString()) && CurrentCell?.RowIndex == NewWatchRowIndex)
                {
                    EndEdit();
                    return true;
                }
            }
            return false;
        }

        private bool HandleEscape()
        {
            if (CurrentCell?.ColumnIndex != NameColumnIndex || !IsCurrentCellInEditMode)
                return false;
            CancelEdit();
            return true;
        }

        #endregion

        #region Standard functions overriding

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_state.ScalingMode == ScalingMode.ResizeColumn
                || _state.ScalingMode == ScalingMode.ResizeQuad
                || _state.ScalingMode == ScalingMode.ResizeHalf) // TODO: move to appropriate place
                _state.RemoveScrollPadding();

            if (_mouseMoveController.OperationDidNotFinishOnMouseUp())
                base.OnMouseDown(e);

            base.OnMouseUp(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var hit = HitTest(e.X, e.Y);
                if (hit.Type == DataGridViewHitTestType.RowHeader)
                    _selectionController.SwitchMode(DataGridViewSelectionMode.RowHeaderSelect);
                if (hit.Type == DataGridViewHitTestType.ColumnHeader)
                    _selectionController.SwitchMode(DataGridViewSelectionMode.ColumnHeaderSelect);
            }
            if (!_mouseMoveController.HandleMouseDown(e))
                base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = DebugVisualizer.MouseMove.ScaleOperation.ShouldChangeCursor(HitTest(e.X, e.Y), _state, e.X)
                ? Cursors.SizeWE : Cursors.Default;
            if (!_mouseMoveController.HandleMouseMove(e))
                base.OnMouseMove(e);
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            if (!_state.TableShouldSuppressOnColumnWidthChangedEvent)
                base.OnColumnWidthChanged(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Trying to handle key with proper custom function
            var handled =
                keyData == Keys.Delete ? HandleDelete() :
                keyData == Keys.Escape ? HandleEscape() :
                keyData == Keys.Enter ? HandleEnter() :
                false;
            // If key is not handled calling base method
            if (!handled)
                return base.ProcessCmdKey(ref msg, keyData);
            // Cmd key succesfully processed
            return true;
        }
        #endregion
    }
}
