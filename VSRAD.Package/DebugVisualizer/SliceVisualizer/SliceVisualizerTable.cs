using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceVisualizerTable : DataGridView
    {
        private const int DataColumnCount = 512;
        private const int PhantomColumnIndex = DataColumnCount;
        public const int DataColumnOffset = 0;

        public SliceWatchWiew SelectedWatch { get; private set; }

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly IFontAndColorProvider _fontAndColor;

        private readonly TableState _state;

        public SliceVisualizerTable(IFontAndColorProvider fontAndColor) : base()
        {
            _fontAndColor = fontAndColor;

            DoubleBuffered = true;
            AllowUserToAddRows = false;

            var dataColumns = SetupColumns();
            Rows.Add(new DataGridViewRow() { Visible = false }); // phantom row for scaling

            _state = new TableState(DataColumnOffset, PhantomColumnIndex, 60, dataColumns, new ColumnResizeController(this));

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            _ = new SliceRowStyling(this);
            _ = new SliceCellStyling(this, fontAndColor);
        }

        private IReadOnlyList<DataGridViewColumn> SetupColumns()
        {
            var dataColumns = new List<DataGridViewColumn>(DataColumnCount);
            for (int i = 0; i < DataColumnCount; i++)
            {
                dataColumns.Add(new DataGridViewTextBoxColumn()
                {
                    HeaderText = i.ToString(),
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Width = 60
                });
                Columns.Add(dataColumns[i]);
            }

            // phantom column
            Columns.Add(new DataGridViewTextBoxColumn()
            {
                MinimumWidth = 2,
                Width = 2,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            return dataColumns;
        }

        public void DisplayWatch(SliceWatchWiew watchWiew)
        {
            SelectedWatch = watchWiew;
            if (Rows.Count < watchWiew.RowCount)
                Rows.AddCopies(0, watchWiew.RowCount - Rows.Count);

            for (int i = 0; i < Rows.Count; i++)
            {
                if (i < watchWiew.RowCount)
                {
                    Rows[i].Visible = true;
                    Rows[i].HeaderCell.Value = i;
                }
                else
                {
                    Rows[i].Visible = false;
                }
            }
            for (int i = 0; i < DataColumnCount; i++)
            {
                if (i < watchWiew.ColumnCount)
                {
                    Columns[i].Visible = true;
                    for (int j = 0; j < watchWiew.RowCount; j++)
                        Rows[j].Cells[i].Value = watchWiew[j, i];
                }
                else
                {
                    Columns[i].Visible = false;
                }
            }
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
