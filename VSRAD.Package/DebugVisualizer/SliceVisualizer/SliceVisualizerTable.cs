using System;
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
        public const int DataColumnOffset = 0;

        public SliceWatchWiew SelectedWatch { get; private set; }
        private int PhantomColumnIndex = DataColumnCount;

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly IFontAndColorProvider _fontAndColor;

        private readonly TableState _state;

        public SliceVisualizerTable(IFontAndColorProvider fontAndColor) : base()
        {
            _fontAndColor = fontAndColor;

            DoubleBuffered = true;
            AllowUserToAddRows = false;
            AutoGenerateColumns = false;

            ColumnAdded += FixFillWeight;

            var dataColumns = SetupColumns();
            Rows.Add(new DataGridViewRow() { Visible = false }); // phantom row for scaling

            _state = new TableState(DataColumnOffset, PhantomColumnIndex, 60, dataColumns, new ColumnResizeController(this));

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            _ = new SliceRowStyling(this);
            _ = new SliceCellStyling(this, fontAndColor);
        }

        private void FixFillWeight(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.FillWeight = 1;
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

        public void DisplayWatch(SliceWatchWiew watchView)
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

            var columnsNeeded = Math.Max(watchView.ColumnCount, Columns.Count); 

            for (int i = 0; i < columnsNeeded; i++)
            {
                if (i == Columns.Count - 1)
                {
                    var column = new DataGridViewTextBoxColumn()
                    {
                        HeaderText = i.ToString(),
                        ReadOnly = true,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = 60
                    };
                    Columns.Insert(i, column);
                    _state.DataColumns.Append(Columns[i]);
                    PhantomColumnIndex++;
                    _state.IncrementPhantomColumnIndex();
                }

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
