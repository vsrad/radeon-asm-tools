using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceVisualizerTable : DataGridView
    {
        public const int DataColumnOffset = 1; // including phantom column

        public TypedSliceWatchView SelectedWatch => _context.SelectedWatchView;
        public bool HeatMapMode { get; private set; }
        public SliceColumnStyling ColumnStyling { get; private set; }

        private readonly SliceVisualizerContext _context;
        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly IFontAndColorProvider _fontAndColor;
        private readonly Options.VisualizerAppearance _appearance;

        private readonly TableState _state;

        public Action<int, string, int> NavigateToCellEvent;

        public SliceVisualizerTable(SliceVisualizerContext context, IFontAndColorProvider fontAndColor, Options.VisualizerAppearance appearance, ColumnStylingOptions stylingOptions) : base()
        {
            _context = context;
            _fontAndColor = fontAndColor;
            _appearance = appearance;

            DoubleBuffered = true;
            AllowUserToAddRows = false;
            AllowUserToResizeColumns = false;
            AllowUserToResizeRows = false;
            AutoGenerateColumns = false;
            HeatMapMode = false;
            ColumnStyling = new SliceColumnStyling(this, _appearance);
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            MouseClick += ShowContextMenu;

            _state = new TableState(this, 60);

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            _ = new SliceRowStyling(this);
            _ = new SliceCellStyling(this, _state, ColumnStyling, fontAndColor, _appearance, stylingOptions);
            ((FontAndColorProvider)_fontAndColor).FontAndColorInfoChanged += FontAndColorChanged;

            _context.WatchSelected += () => DisplayWatch(_context.SelectedWatchView, _context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns);
            _context.HeatMapStateChanged += (e, mode) => SetHeatMapMode(mode);
            _context.DivierWidthChanged += () => ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
            _context.ColorRangeChanged += () => Invalidate();
        }

        private void ShowContextMenu(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = HitTest(e.X, e.Y);

            var col = hit.ColumnIndex - DataColumnOffset;
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

        public void DisplayWatch(TypedSliceWatchView watchView, int subgroupSize, string columnSelector)
        {
            var columnsMissing = watchView.ColumnCount - (Columns.Count - 1);
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
                }
                _state.AddDataColumns(columns);
                Debug.Assert(_state.DataColumnOffset == DataColumnOffset);
            }
            for (int i = 0; i < _state.DataColumns.Count; ++i)
                _state.DataColumns[i].Visible = i < watchView.ColumnCount;

            if (Rows.Count < watchView.RowCount)
                Rows.Add(watchView.RowCount - Rows.Count);
            for (int i = 0; i < Rows.Count; ++i)
            {
                var row = Rows[i];
                if (i < watchView.RowCount)
                {
                    for (int j = 0; j < watchView.ColumnCount; ++j)
                        row.Cells[DataColumnOffset + j].Value = watchView[i, j];
                    row.HeaderCell.Value = watchView.RowHeader(i);
                    row.Visible = true;
                }
                else
                {
                    row.Visible = false;
                }
            }
            ColumnStyling.Recompute(subgroupSize, columnSelector, _appearance);
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            if (!_state.TableShouldSuppressOnColumnWidthChangedEvent)
                base.OnColumnWidthChanged(e);
        }

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
        #endregion
    }
}
