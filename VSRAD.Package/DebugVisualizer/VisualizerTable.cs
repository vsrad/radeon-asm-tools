using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class VisualizerTable : DataGridView
    {
        public delegate void ChangeWatchState(IEnumerable<Watch> newState, IEnumerable<DataGridViewRow> invalidatedRows);
        public delegate uint GetGroupSize();

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

        private bool _watchDataValid = true;
        /// <summary>Set externally by the context to indicate whether the cell values are valid or should be grayed out.</summary>
        public bool WatchDataValid { get => _watchDataValid; set { _watchDataValid = value; Invalidate(); } }

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;

        private readonly IFontAndColorProvider _fontAndColor;
        private readonly ComputedColumnStyling _computedStyling;

        private string _editedWatchName;

        private readonly TableState _state;

        private bool _hostWindowHasFocus;
        private bool _enterPressedOnWatchEndEdit;

        public VisualizerTable(ProjectOptions options, IFontAndColorProvider fontAndColor) : base()
        {
            _fontAndColor = fontAndColor;
            _computedStyling = new ComputedColumnStyling();

            RowHeadersWidth = 45;
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
            CellClick += (sender, args) => { if (args.RowIndex == NewWatchRowIndex) BeginEdit(false); };

            // Custom pan/scale cursors
            GiveFeedback += (sender, e) => e.UseDefaultCursors = false;

            ShowCellToolTips = false;
            DoubleBuffered = true;
            AllowUserToResizeRows = false;
            EnableHeadersVisualStyles = false; // custom font and color settings for cell headers

            _state = new TableState(this, columnWidth: 60, NameColumnIndex);
            _state.NameColumnScalingEnabled = true;
            SetupColumns();

            Debug.Assert(_state.DataColumnOffset == DataColumnOffset);
            Debug.Assert(_state.PhantomColumnIndex == PhantomColumnIndex);

            _ = new ContextMenus.ContextMenuController(this, new ContextMenus.IContextMenu[]
            {
                new ContextMenus.TypeContextMenu(this,
                VariableTypeChanged,
                ProcessCopy,
                InsertSeparatorRow,
                addWatchRange: (name, from, to) => ArrayRange.FormatArrayRangeWatch(name, from, to, options.VisualizerOptions.MatchBracketsOnAddToWatches).ToList().ForEach(AddWatch),
                PromoteToWatch),
                new ContextMenus.CopyContextMenu(this, ProcessCopy),
                new ContextMenus.SubgroupContextMenu(this, _state, options.VisualizerColumnStyling, () => options.DebuggerOptions.GroupSize)
            });
            _ = new CellStyling(this, options.VisualizerAppearance, _computedStyling, _fontAndColor);
            _ = new CustomTableGraphics(this);

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
        }

        public void AddWatch(string watchName)
        {
            var watchRow = InsertUserWatchRow(new Watch(watchName, VariableType.Default), NewWatchRowIndex);
            RaiseWatchStateChanged(new[] { watchRow });
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
            var changedRows = _selectionController.GetClickTargetRows(rowIndex).ToList();
            changedRows.AddRange(Rows.Cast<DataGridViewRow>().Where(r => ((WatchNameCell)r.Cells[NameColumnIndex]).ParentRows.Any(changedRows.Contains)));
            foreach (var row in changedRows)
                row.HeaderCell.Value = type.ShortName();
            RaiseWatchStateChanged(changedRows);
        }

        public void HostWindowFocusChanged(bool hasFocus)
        {
            _hostWindowHasFocus = hasFocus;
            if (hasFocus)
            {
                DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            }
            else
            {
                DefaultCellStyle.SelectionBackColor = Color.LightSteelBlue;
                EndEdit();
            }
        }

        public bool IsUserWatchRow(DataGridViewRow r) =>
            r.Index > SystemRowIndex && r.Index < NewWatchRowIndex && ((WatchNameCell)r.Cells[NameColumnIndex]).NestingLevel == 0;

        public bool IsListItemRow(DataGridViewRow r) =>
            r.Index > SystemRowIndex && r.Index < NewWatchRowIndex && ((WatchNameCell)r.Cells[NameColumnIndex]).NestingLevel > 0;

        public IEnumerable<DataGridViewRow> GetUserWatchRows() =>
            Rows.Cast<DataGridViewRow>().Where(IsUserWatchRow);

        public IEnumerable<DataGridViewRow> GetSelectedUserWatchRows() =>
            GetUserWatchRows().Where(r => r.Cells[NameColumnIndex].Selected);

        public IEnumerable<Watch> GetCurrentWatchState() =>
            GetUserWatchRows().Select(GetRowWatchState);

        public static Watch GetRowWatchState(DataGridViewRow row) => new Watch(
            name: row.Cells[NameColumnIndex].Value?.ToString(),
            type: VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString()));

        private void RaiseWatchStateChanged(IEnumerable<DataGridViewRow> invalidatedRows) =>
            WatchStateChanged(GetCurrentWatchState(), invalidatedRows);

        private void UpdateEditedWatchName(object sender, EventArgs e)
        {
            if (CurrentCell?.ColumnIndex == NameColumnIndex && CurrentCell.RowIndex != NewRowIndex && CurrentCell.Value != null)
                _editedWatchName = CurrentCell.Value.ToString();
        }

        private void InsertSeparatorRow(int rowIndex, bool after)
        {
            var index = after ? rowIndex + 1 : rowIndex;
            var separatorRow = InsertUserWatchRow(new Watch(" ", VariableType.Default), index);
            RaiseWatchStateChanged(new[] { separatorRow });
        }

        public DataGridViewRow InsertUserWatchRow(Watch watch, int index = -1, bool canBeRemoved = true)
        {
            if (index == -1)
                index = RowCount;

            Rows.Insert(index);
            var insertedRow = Rows[index];

            insertedRow.Cells[NameColumnIndex].Value = watch.Name;
            insertedRow.Cells[NameColumnIndex].ReadOnly = !canBeRemoved;
            insertedRow.HeaderCell.Value = watch.Info.ShortName();

            var currentWidth = Columns[NameColumnIndex].Width;
            var preferredWidth = Columns[NameColumnIndex].GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            if (preferredWidth > currentWidth)
                Columns[NameColumnIndex].Width = preferredWidth;

            return insertedRow;
        }

        public void PrepareNewWatchRow()
        {
            var newRowIndex = Rows.Add();
            Rows[newRowIndex].Cells[NameColumnIndex].ReadOnly = false;
            Rows[newRowIndex].HeaderCell.Value = "";
            ClearSelection();
        }

        private void PromoteToWatch(int rowIndex)
        {
            var userWatchRows = GetUserWatchRows().ToList();
            var rowsToPromote = _selectionController.GetClickTargetRows(rowIndex);
            var insertedRows = new List<DataGridViewRow>();
            foreach (var row in rowsToPromote)
            {
                if (row.Cells[NameColumnIndex] is WatchNameCell nameCell && nameCell.NestingLevel > 0)
                {
                    var type = VariableTypeUtils.TypeFromShortName(row.HeaderCell.Value.ToString());
                    var parentIdxInUserRows = userWatchRows.IndexOf(nameCell.ParentRows[0]);
                    var rowIdxAfterParent = parentIdxInUserRows + 1 < userWatchRows.Count ? userWatchRows[parentIdxInUserRows + 1].Index : NewWatchRowIndex;
                    insertedRows.Add(InsertUserWatchRow(new Watch(nameCell.FullWatchName, type), rowIdxAfterParent));
                }
            }
            RaiseWatchStateChanged(insertedRows);
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
            Columns.Add(new WatchNameColumn
            {
                HeaderText = "Name",
                ReadOnly = false,
                Frozen = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            CreateMissingDataColumns(DataColumnCount);
        }

        private void WatchEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Asynchronous delegate to prevent reentrant calls inside WatchEndEdit (it will be called again when changing the focus to another cell, incl. when removing the current row)
            // See also https://social.msdn.microsoft.com/Forums/windows/en-US/f824fbbf-9d08-4191-98d6-14903801acfc/operation-is-not-valid-because-it-results-in-a-reentrant-call-to-the-setcurrentcelladdresscore
            BeginInvoke(new MethodInvoker(() =>
            {
                var row = Rows[e.RowIndex];
                var rowWatchName = (string)row.Cells[NameColumnIndex].Value;

                var nextWatchIndex = -1;
                var shouldMoveCaretToNextWatch = _hostWindowHasFocus && _enterPressedOnWatchEndEdit;
                _enterPressedOnWatchEndEdit = false; // Don't move the focus again on successive WatchEndEdit invocations

                if (e.RowIndex == NewWatchRowIndex) // Adding a new watch
                {
                    if (!string.IsNullOrWhiteSpace(rowWatchName))
                    {
                        row.Cells[NameColumnIndex].Value = rowWatchName.Trim();
                        row.HeaderCell.Value = new VariableType(VariableCategory.Hex, 32).ShortName();
                        PrepareNewWatchRow();
                        RaiseWatchStateChanged(new[] { row });
                        if (shouldMoveCaretToNextWatch)
                            nextWatchIndex = NewWatchRowIndex;
                    }
                }
                else if (e.RowIndex != 0) // Modifying an existing watch
                {
                    if (!string.IsNullOrEmpty(rowWatchName)) // Allow watch names consisting only of whitespace characters for divider rows
                    {
                        if (rowWatchName != _editedWatchName)
                            RaiseWatchStateChanged(new[] { row });
                        if (shouldMoveCaretToNextWatch)
                            nextWatchIndex = Rows.Cast<DataGridViewRow>().FirstOrDefault(r => IsUserWatchRow(r) && r.Index > e.RowIndex)?.Index ?? NewWatchRowIndex;
                    }
                    else
                    {
                        Rows.RemoveAt(e.RowIndex);
                        RaiseWatchStateChanged(Enumerable.Empty<DataGridViewRow>());
                        if (shouldMoveCaretToNextWatch)
                            nextWatchIndex = Rows.Cast<DataGridViewRow>().LastOrDefault(r => IsUserWatchRow(r) && r.Index <= e.RowIndex)?.Index ?? 0;
                    }
                }

                if (!_hostWindowHasFocus)
                {
                    ClearSelection();
                }
                if (nextWatchIndex != -1)
                {
                    CurrentCell = Rows[nextWatchIndex].Cells[NameColumnIndex];
                    BeginEdit(true);
                }
            }));
        }

        #region Styling

        public void ApplyRowHighlight(int rowIndex, DataHighlightColor? changeFg = null, DataHighlightColor? changeBg = null)
        {
            foreach (var row in _selectionController.GetClickTargetRows(rowIndex))
            {
                if (changeFg is DataHighlightColor newFg)
                    row.DefaultCellStyle.ForeColor = (newFg != DataHighlightColor.None) ? _fontAndColor.FontAndColorState.HighlightForeground[(int)newFg] : Color.Empty;
                if (changeBg is DataHighlightColor newBg)
                    row.DefaultCellStyle.BackColor = (newBg != DataHighlightColor.None) ? _fontAndColor.FontAndColorState.HighlightBackground[(int)newBg] : Color.Empty;
            }
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

        public void ApplyDataStyling(ProjectOptions options, Server.BreakStateData breakData)
        {
            ((Control)this).SuspendDrawing();
            _disableColumnWidthChangeHandler = true;

            CreateMissingDataColumns((int)options.DebuggerOptions.GroupSize);

            _computedStyling.Recompute(options.VisualizerOptions, options.VisualizerAppearance, options.VisualizerColumnStyling,
                options.DebuggerOptions.GroupSize, options.DebuggerOptions.WaveSize, breakData);

            ApplyFontAndColorInfo();

            var columnStyling = new ColumnStyling(
                options.VisualizerAppearance,
                options.VisualizerColumnStyling,
                _computedStyling,
                _fontAndColor.FontAndColorState);
            columnStyling.Apply(_state.DataColumns);

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
                var selectedRows = GetSelectedUserWatchRows().ToList();
                var rowsToDelete = Rows.Cast<DataGridViewRow>()
                    .Where(r => selectedRows.Contains(r) || selectedRows.Contains(((WatchNameCell)r.Cells[NameColumnIndex]).ParentRows.FirstOrDefault()))
                    .ToList(); // Need to materialize the collection prior to removing any rows
                foreach (var row in rowsToDelete)
                    Rows.Remove(row);

                RaiseWatchStateChanged(Enumerable.Empty<DataGridViewRow>());

                return true;
            }

            return false;
        }

        private bool HandleEnter()
        {
            if (CurrentCell?.ColumnIndex == NameColumnIndex && IsUserWatchRow(CurrentRow))
            {
                if (!IsCurrentCellInEditMode)
                {
                    BeginEdit(false);
                    return true;
                }
                _enterPressedOnWatchEndEdit = true;
            }
            return false;
        }

        private bool HandleEscape()
        {
            if (CurrentCell?.ColumnIndex == NameColumnIndex && IsCurrentCellInEditMode)
            {
                CancelEdit();
                return true;
            }
            return false;
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
