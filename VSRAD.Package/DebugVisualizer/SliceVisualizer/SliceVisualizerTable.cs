using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceVisualizerTable : DataGridView
    {
        private const int DataColumnCount = 512;
        private const int PhantomColumnIndex = DataColumnCount;

        private readonly MouseMove.MouseMoveController _mouseMoveController;
        private readonly SelectionController _selectionController;
        private readonly FontAndColorProvider _fontAndColor;

        private TableState _state;

        public SliceVisualizerTable(FontAndColorProvider fontAndColor) : base()
        {
            _fontAndColor = fontAndColor;

            AllowUserToAddRows = false;

            var dataColumns = SetupColumns();
            Rows.Add(new DataGridViewRow() { Visible = false }); // phantom row for scaling

            _state = new TableState(0, PhantomColumnIndex, 60, dataColumns, new ColumnResizeController(this));

            _mouseMoveController = new MouseMove.MouseMoveController(this, _state);
            _selectionController = new SelectionController(this);
            _ = new CustomSliceTableGraphics(this);
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

            Columns.Add(new DataGridViewTextBoxColumn()
            {
                MinimumWidth = 2,
                Width = 2,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Visible = true
            });
            Columns[PhantomColumnIndex].DefaultCellStyle.BackColor = System.Drawing.ColorTranslator.FromHtml("#ABABAB");
            Columns[PhantomColumnIndex].HeaderCell.Style.BackColor = System.Drawing.ColorTranslator.FromHtml("#ABABAB");
            Columns[PhantomColumnIndex].ReadOnly = true;
            return dataColumns;
        }

        public void ApplyDataStyling(Options.ProjectOptions options/*uint groupSize*/)
        {
            // there will be separate options appearance and styling for slice
            var columnStyling = new ColumnStyling(options.VisualizerOptions, options.VisualizerAppearance, options.VisualizerColumnStyling, _fontAndColor.FontAndColorState);
            columnStyling.ApplyHeatMap(Rows.Cast<DataGridViewRow>().ToList(), Color.White, Color.White);
        }

        public void DisplayWatch(List<uint[]> data)
        {
            Rows.Clear();

            for (int i = 1; i <= data.Count; i++)
            {
                var index = Rows.Add(new DataGridViewRow());
                Rows[index].HeaderCell.Value = i;
            }
            
            for (int i = 0; i < DataColumnCount; i++)
            {
                if (i < data[0].Length)
                {
                    for (int j = 0; j < data.Count; j++)
                        Rows[j].Cells[i].Value = data[j][i];
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
