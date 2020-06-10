using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceVisualizerTable : DataGridView
    {
        public const int DataColumnOffset = 0;

        public TypedSliceWatchView SelectedWatch { get; private set; }

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
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            ColumnAdded += FixFillWeight;

            // Phantom column for scaling
            Columns.Add(new DataGridViewTextBoxColumn()
            {
                MinimumWidth = 2,
                Width = 2,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            // Scaling requires at least one row in the table, we'll reuse it later for data
            Rows.Add(new DataGridViewRow() { Visible = false });

            _state = new TableState(DataColumnOffset, 60, new List<DataGridViewColumn>(), new ColumnResizeController(this));

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            _ = new SliceRowStyling(this);
            _ = new SliceCellStyling(this, fontAndColor);
        }

        private void FixFillWeight(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.FillWeight = 1;
        }

        public void DisplayWatch(TypedSliceWatchView watchView)
        {
            SelectedWatch = watchView;
            if (Rows.Count < watchView.RowCount)
                Rows.AddCopies(0, watchView.RowCount - Rows.Count);

            // TODO: handle odd number of rows
            for (int i = 0; i < Rows.Count; i++)
            {
                if (i < watchView.RowCount)
                {
                    Rows[i].Visible = true;
                    Rows[i].HeaderCell.Value = i;
                }
                else
                {
                    Rows[i].Visible = false;
                }
            }

            // Mind the phantom column at the end!
            var columnsMissing = watchView.ColumnCount - (Columns.Count - 1);
            if (columnsMissing > 0)
            {
                var missingColumnsStartAt = _state.PhantomColumnIndex;
                var columns = new DataGridViewColumn[columnsMissing];
                for (int i = 0; i < columnsMissing; ++i)
                {
                    columns[i] = new DataGridViewTextBoxColumn()
                    {
                        HeaderText = (missingColumnsStartAt + i).ToString(),
                        ReadOnly = true,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = 60
                    };
                }
                _state.DataColumns.AddRange(columns);
                Columns.AddRange(columns);

                // Put phantom column at the end
                var oldPhantomColumn = Columns[missingColumnsStartAt];
                Columns.Remove(oldPhantomColumn);
                oldPhantomColumn.DisplayIndex = -1;
                Columns.Add(oldPhantomColumn);
            }
            var totalColumns = Columns.Count - 1;
            for (int i = 0; i < totalColumns; i++)
            {
                if (i < watchView.ColumnCount)
                {
                    Columns[i].Visible = true;
                    for (int j = 0; j < watchView.RowCount; j++)
                        Rows[j].Cells[i].Value = watchView[j, i];
                }
                else
                {
                    Columns[i].Visible = false;
                }
            }
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            if (!_state.ResizeController.HandleColumnWidthChangeEvent())
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
            Cursor = DebugVisualizer.MouseMove.ScaleOperation.ShouldChangeCursor(HitTest(e.X, e.Y), this, _state, e.X)
                ? Cursors.SizeWE : Cursors.Default;
            if (!_mouseMoveController.HandleMouseMove(e))
                base.OnMouseMove(e);
        }
        #endregion
    }
}
