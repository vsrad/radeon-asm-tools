using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceVisualizerTable : DataGridView
    {
        public const int DataColumnOffset = 2; // including phantom column and first invisible column

        public TypedSliceWatchView SelectedWatch => _context.SelectedWatchView;
        public bool HeatMapMode { get; private set; }
        public SliceColumnStyling ColumnStyling { get; private set; }

        public const int GroupNumberColumnIndex = 0;
        public int ReservedColumnsOffset => Columns[GroupNumberColumnIndex].Width + 1; // +1 for border

        private readonly SliceVisualizerContext _context;
        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly IFontAndColorProvider _fontAndColor;

        private readonly TableState _state;

        public uint GroupSize => _context.GroupSize;
        public int PhantomColumnIndex => _state.PhantomColumnIndex;

        public SliceVisualizerTable(SliceVisualizerContext context, IFontAndColorProvider fontAndColor) : base()
        {
            _context = context;
            _fontAndColor = fontAndColor;

            DoubleBuffered = true;
            AllowUserToAddRows = false;
            AllowUserToResizeColumns = false;
            AllowUserToResizeRows = false;
            AutoGenerateColumns = false;
            VirtualMode = true;
            RowHeadersVisible = false;
            CellValueNeeded += DisplayCellValue;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            MouseClick += ShowContextMenu;
            CellMouseEnter += DisplayCellStatus;
            MouseLeave += (s, e) => _context.StatusString = ""; // clear status bar on leaving control

            HeatMapMode = _context.Options.SliceVisualizerOptions.UseHeatMap;
            ColumnStyling = new SliceColumnStyling(this, _context.Options.VisualizerAppearance);

            SetupColumns();
            _state = new TableState(this, 60);
            _state.AddDataColumns(Array.Empty<DataGridViewColumn>());

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            SliceRowStyling.ApplyOnRowPostPaint(this);
            SliceCellStyling.ApplyCellStylingOnCellPainting(this, ColumnStyling, fontAndColor, _context);
            ((FontAndColorProvider)_fontAndColor).FontAndColorInfoChanged += FontAndColorChanged;

            _context.WatchSelected += DisplayWatch;
            _context.Options.VisualizerColumnStyling.PropertyChanged += ColumnStylingChanged;
            _context.Options.VisualizerAppearance.PropertyChanged += AppearanceOptionChanged;
            _context.Options.SliceVisualizerOptions.PropertyChanged += SliceOptionChanged;

            _state.ScalingMode = _context.Options.VisualizerAppearance.ScalingMode;

            ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
        }

        private void DisplayCellValue(object sender, DataGridViewCellValueEventArgs e)
        {
            var dataColumnIndex = e.ColumnIndex - DataColumnOffset;
            if (SelectedWatch == null || e.RowIndex >= SelectedWatch.RowCount)
                return;

            if (e.ColumnIndex == 0) // Group #
                e.Value = SelectedWatch.GetGroupIndex(row: e.RowIndex, column: 0);
            else if (dataColumnIndex >= 0 && dataColumnIndex < SelectedWatch.ColumnCount)
                e.Value = SelectedWatch[e.RowIndex, e.ColumnIndex - DataColumnOffset];
        }

        private void SetupColumns()
        {
            // 1) Used as a workaround for scaling logic relying on a fixed column at the start of the table
            // 2) Accessing HeaderCells for each row is too expensive when the row count is over 1000 so we use our own column to display group indexes
            var idx = Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Group #",
                FillWeight = 1,
                Width = 75,
                ReadOnly = true,
                Frozen = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            Columns[idx].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            Columns[idx].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void DisplayCellStatus(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var info = SelectedWatch.AllValuesEqual && HeatMapMode
                ? "*Note: HeatMap mode is active, but all values of current watch are equal accross all threads."
                : "";
            _context.SetStatusString(e.RowIndex, e.ColumnIndex - DataColumnOffset, Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString(), info);
        }

        private void ShowContextMenu(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = HitTest(e.X, e.Y);
            if (hit.Type != DataGridViewHitTestType.Cell) return;
            if (hit.ColumnIndex < DataColumnOffset || hit.ColumnIndex == PhantomColumnIndex) return;

            var col = hit.ColumnIndex - DataColumnOffset;
            if (SelectedWatch.IsInactiveCell(hit.RowIndex, col)) return;

            new ContextMenu(new MenuItem[] {
                new MenuItem("Go to watch list", (s, o) => _context.NavigateToCell(hit.RowIndex, col))
            }).Show(this, new Point(e.X, e.Y));
        }

        private void FontAndColorChanged() => Invalidate();

        public void SetHeatMapMode(bool value)
        {
            HeatMapMode = value;
            Invalidate();   // redraw
        }

        public void DisplayWatch()
        {
            if (SelectedWatch.ColumnCount > 8192)
            {
                Errors.ShowException(
                    new ArgumentException("The column count in Slice Visualizer exceeded the limit - 8192 columns. " +
                        "Please check your configuration: Group Size and Groups in Row.")
                );
                return;
            }
            var columnsMissing = SelectedWatch.ColumnCount - _state.DataColumns.Count;
            if (columnsMissing > 0)
            {
                var missingColumnsStartAt = _state.DataColumns.Count;
                var columns = new DataGridViewColumn[columnsMissing];
                for (int i = 0; i < columnsMissing; ++i)
                {
                    columns[i] = new DataGridViewTextBoxColumn
                    {
                        FillWeight = 1,
                        HeaderText = (missingColumnsStartAt + i).ToString(),
                        ReadOnly = true,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = _state.ColumnWidth
                    };
                    columns[i].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    columns[i].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                _state.AddDataColumns(columns);
                Debug.Assert(_state.DataColumnOffset == DataColumnOffset);
            }
            for (int i = 0; i < _state.DataColumns.Count; ++i)
                _state.DataColumns[i].Visible = i < SelectedWatch.ColumnCount;

            ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);

            RowCount = SelectedWatch.RowCount;
            Invalidate();
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            if (!_state.TableShouldSuppressOnColumnWidthChangedEvent)
                base.OnColumnWidthChanged(e);
        }
        #region property changed handlers
        private void SliceOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.UseHeatMap):
                    SetHeatMapMode(_context.Options.SliceVisualizerOptions.UseHeatMap);
                    break;
            }
        }

        private void AppearanceOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.VisualizerAppearance.SliceHiddenColumnSeparatorWidth):
                case nameof(Options.VisualizerAppearance.SliceSubgroupSeparatorWidth):
                    ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
                    break;
                case nameof(Options.VisualizerAppearance.ScalingMode):
                    _state.ScalingMode = _context.Options.VisualizerAppearance.ScalingMode;
                    break;
            }
        }

        private void ColumnStylingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(_context.Options.VisualizerColumnStyling.BackgroundColors):
                    Invalidate();
                    break;
            }
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
            if (e.Button == MouseButtons.Left)
            {
                var hit = HitTest(e.X, e.Y);
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
        #endregion
    }
}
