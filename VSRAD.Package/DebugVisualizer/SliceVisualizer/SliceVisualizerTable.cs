using System.Diagnostics;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceVisualizerTable : DataGridView
    {
        public const int DataColumnOffset = 1; // including phantom column

        public TypedSliceWatchView SelectedWatch { get; private set; }
        public bool HeatMapMode { get; private set; }

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly IFontAndColorProvider _fontAndColor;

        private readonly TableState _state;

        public SliceVisualizerTable(IFontAndColorProvider fontAndColor) : base()
        {
            _fontAndColor = fontAndColor;

            DoubleBuffered = true;
            AllowUserToAddRows = false;
            AllowUserToResizeColumns = false;
            AllowUserToResizeRows = false;
            AutoGenerateColumns = false;
            HeatMapMode = false;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            _state = new TableState(this, 60, 0);
            _state.NameColumnScalingEnabled = false; // slice does not have name column
#if false // TODO FIXME
            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
#endif
            _ = new SliceRowStyling(this);
            _ = new SliceCellStyling(this, _state, fontAndColor);
        }

        public void SetHeatMapMode(bool value)
        {
            HeatMapMode = value;
            Invalidate();   // redraw
        }

        public void DisplayWatch(TypedSliceWatchView watchView)
        {
            SelectedWatch = watchView;

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
                    row.HeaderCell.Value = i;
                    row.Visible = true;
                }
                else
                {
                    row.Visible = false;
                }
            }
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
