using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class VisualizerTable : DataGridView
    {
        public delegate void ChangeWatchState(List<Watch> newState, IEnumerable<DataGridViewRow> invalidatedRows);
        public delegate int GetGroupSize();

        public event ChangeWatchState WatchStateChanged;

        public const int NameColumnIndex = 0;
        public const int DataColumnOffset = 1; /* name column */
        public const int DataColumnCount = 512;

        public int NewWatchRowIndex => RowCount - 1; /* new watches are always entered in the last row */
        public int GroupSize => _groupSizeGetter();
        public int ReservedColumnsOffset => RowHeadersWidth + Columns[NameColumnIndex].Width;

        #region Appearance
        public int HiddenColumnSeparatorWidth = 8;
        public SolidBrush HiddenColumnSeparatorColor;
        public uint LaneGrouping;
        public int LaneSeparatorWidth = 3;
        public SolidBrush LaneSeparatorColor;
        public ScalingMode ScalingMode = ScalingMode.ResizeColumn;
        #endregion

        public bool ShowSystemRow
        {
            get => Rows.Count > 0 && Rows[0].Visible;
            set { if (Rows.Count > 0) Rows[0].Visible = value; }
        }

        //public ContentAlignment NameColumnAlignment = ContentAlignment.Left;
        //public ContentAlignment DataColumnAlignment = ContentAlignment.Left;

        public IReadOnlyList<DataGridViewColumn> DataColumns { get; }
        public IEnumerable<DataGridViewRow> DataRows => Rows
            .Cast<DataGridViewRow>()
            .Where(x => x.Index > 0 && x.Index != NewWatchRowIndex);

        public ColumnResizeController ColumnResizeController { get; }

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;

        private readonly ColumnStylingOptions _stylingOptions;
        private readonly GetGroupSize _groupSizeGetter;

        private string _editedWatchName;

        private readonly FontAndColorProvider _fontAndColor;

        public VisualizerTable(ColumnStylingOptions options, GetGroupSize groupSizeGetter) : base()
        {
            _stylingOptions = options;
            _groupSizeGetter = groupSizeGetter;

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

            ColumnResizeController = new ColumnResizeController(this);

            _ = new ContextMenus.ContextMenuController(this, new ContextMenus.IContextMenu[]
            {
                new ContextMenus.TypeContextMenu(this, VariableTypeChanged, AvgprStateChanged, FontColorChanged, ProcessCopy, InsertSeparatorRow),
                new ContextMenus.CopyContextMenu(this, ProcessCopy),
                new ContextMenus.SubgroupContextMenu(this, ColumnSelectorChanged, ColorClicked)
            });
            _ = new CustomTableGraphics(this);

            _mouseMoveController = new MouseMove.MouseMoveController(this);
            _selectionController = new SelectionController(this);

            DataColumns = SetupColumns();

            EnableHeadersVisualStyles = false;
            _fontAndColor = new FontAndColorProvider();
            _fontAndColor.FontAndColorInfoChanged += ApplyFontAndColorInfo;
            ApplyFontAndColorInfo();
        }

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

        public void HideColumns(int clickedColumnIndex)
        {
            var selectedColumns = SelectedColumns
                .Cast<DataGridViewColumn>()
                .Select(x => x.Index - DataColumnOffset)
                .Where(x => x >= 0)
                .Append(clickedColumnIndex - DataColumnOffset);

            _stylingOptions.VisibleColumns =
                ColumnSelector.FromIndexes(
                    ColumnSelector.ToIndexes(_stylingOptions.VisibleColumns)
                    .Where(x => !selectedColumns.Contains(x))
                );

            ClearSelection();
        }

        private void ProcessCopy()
        {
            ProcessInsertKey(Keys.Control | Keys.C);
        }

        private void VariableTypeChanged(int rowIndex, VariableType type)
        {
            var changedRows = _selectionController.SelectedWatchIndexes.Append(rowIndex).Select(i => Rows[i]);
            foreach (var row in changedRows)
                row.HeaderCell.Value = type.ShortName();
            RaiseWatchStateChanged(changedRows);
        }

        public void FontColorChanged(int rowIndex, Color color)
        {
            var changedRows = _selectionController.SelectedWatchIndexes.Append(rowIndex).Select(i => Rows[i]);
            RowStyling.ChangeRowFontColor(changedRows, color);
        }

        private void AvgprStateChanged(int rowIndex, bool newAvgprState)
        {
            var selectedNames = _selectionController.SelectedWatchIndexes.Append(rowIndex).Select(i => Rows[i].Cells[NameColumnIndex].Value.ToString());
            var selectedRows = DataRows.Where(r => selectedNames.Contains(r.Cells[NameColumnIndex].Value.ToString()));
            foreach (var row in selectedRows)
                row.HeaderCell.Tag = newAvgprState;
            RaiseWatchStateChanged(selectedRows);
            Invalidate(); // redraw custom avgpr graphics
        }

        public void ColumnSelectorChanged(string newSelector)
        {
            _stylingOptions.VisibleColumns =
                ColumnSelector.GetSelectorMultiplication(_stylingOptions.VisibleColumns, newSelector);
            ClearSelection();
        }

        public void HostWindowDeactivated()
        {
            CancelEdit();
        }

        public void ColorClicked(int clickedColumnIndex, ColumnHighlightColor? color)
        {
            var selectedColumns = SelectedColumns
                .Cast<DataGridViewColumn>()
                .Select(x => x.Index - DataColumnOffset)
                .Where(x => x >= 0)
                .Append(clickedColumnIndex - DataColumnOffset);

            ColumnSelector.RemoveIndexes(selectedColumns, _stylingOptions.HighlightRegions);

            if (color == null)
            {
                ClearSelection();
                return;
            }

            var selector = ColumnSelector.FromIndexes(selectedColumns);

            var existingRegion = _stylingOptions.HighlightRegions.FirstOrDefault(r => r.Selector == selector);
            if (existingRegion != null)
            {
                if (color == null)
                    _stylingOptions.HighlightRegions.Remove(existingRegion);
                else
                    existingRegion.Color = color.Value;
            }
            else if (color != null)
            {
                _stylingOptions.HighlightRegions.Add(new ColumnHighlightRegion { Color = color.Value, Selector = selector });
            }
            ClearSelection();
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
            RaiseWatchStateChanged(new[] { Rows[index] });
        }

        public void AppendVariableRow(Watch watch, bool canBeRemoved = true)
        {
            var index = Rows.Add();
            Rows[index].Cells[NameColumnIndex].Value = watch.Name;
            Rows[index].HeaderCell.Value = watch.Type.ShortName();
            Rows[index].HeaderCell.Tag = watch.IsAVGPR;
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

        private IReadOnlyList<DataGridViewColumn> SetupColumns()
        {
            Columns.Add(new DataGridViewTextBoxColumn()
            {
                HeaderText = "Name",
                ReadOnly = false,
                Frozen = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            var dataColumns = new List<DataGridViewColumn>(DataColumnCount);
            for (int i = 0; i < DataColumnCount; i++)
            {
                dataColumns.Add(new DataGridViewTextBoxColumn()
                {
                    HeaderText = i.ToString(),
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                Columns.Add(dataColumns[i]);
            }
            return dataColumns;
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

        public void AlignmentChanged(
                ContentAlignment nameColumnAlignment,
                ContentAlignment dataColumnAlignment,
                ContentAlignment headersAlignment)
        {
            Columns[0].DefaultCellStyle.Alignment = nameColumnAlignment.AsDataGridViewContentAlignment();
            Columns[0].HeaderCell.Style.Alignment = headersAlignment.AsDataGridViewContentAlignment();
            foreach (var column in DataColumns)
            {
                column.DefaultCellStyle.Alignment = dataColumnAlignment.AsDataGridViewContentAlignment();
                column.HeaderCell.Style.Alignment = headersAlignment.AsDataGridViewContentAlignment();
            }
        }

        private void ApplyFontAndColorInfo()
        {
            var (headerFont, headerFg) = _fontAndColor.GetInfo(FontAndColorItem.Header, DefaultFont);
            var (dataFont, dataFg) = _fontAndColor.GetInfo(FontAndColorItem.Data, DefaultFont);
            var (watchFont, watchFg) = _fontAndColor.GetInfo(FontAndColorItem.WatchNames, DefaultFont);

            ColumnHeadersDefaultCellStyle.Font = headerFont;
            ColumnHeadersDefaultCellStyle.ForeColor = headerFg;

            RowHeadersDefaultCellStyle.Font = watchFont;
            RowHeadersDefaultCellStyle.ForeColor = watchFg;
            Columns[0].DefaultCellStyle.Font = watchFont;
            Columns[0].DefaultCellStyle.ForeColor = watchFg;

            foreach (var column in DataColumns)
            {
                column.DefaultCellStyle.Font = dataFont;
                column.DefaultCellStyle.ForeColor = dataFg;
            }
        }

        #endregion

        #region Custom CmdKeys handlers

        private bool HandleDelete()
        {
            if (!IsCurrentCellInEditMode)
            {
                // deleting System is forbidden
                var selectedRows = _selectionController.SelectedWatchIndexes.Where(i => i != 0).Select(i => Rows[i]);
                foreach (var row in selectedRows)
                    Rows.Remove(row);

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
            if (_mouseMoveController.OperationDidNotFinishOnMouseUp())
                base.OnMouseDown(e);

            base.OnMouseUp(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_mouseMoveController.HandleMouseDown(e))
                base.OnMouseDown(e);
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            if (!ColumnResizeController.HandleColumnWidthChangeEvent())
                base.OnColumnWidthChanged(e);
        }

        protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
        {
            _selectionController.ColumnHeaderClicked(e.ColumnIndex);
            base.OnColumnHeaderMouseClick(e);
        }

        protected override void OnRowHeaderMouseClick(DataGridViewCellMouseEventArgs e)
        {
            _selectionController.RowHeaderClicked(e.RowIndex);
            base.OnRowHeaderMouseClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = DebugVisualizer.MouseMove.ScaleOperation.ShouldChangeCursor(HitTest(e.X, e.Y), this, e.X)
                ? Cursors.SizeWE : Cursors.Default;
            if (!_mouseMoveController.HandleMouseMove(e))
                base.OnMouseMove(e);
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
