using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public class WatchNameColumn : DataGridViewColumn
    {
        public WatchNameColumn() : base(new WatchNameCell()) { }
    }

    public class WatchNameCell : DataGridViewTextBoxCell
    {
        public int IndexInList { get; set; } = -1;
        public List<DataGridViewRow> ParentRows { get; } = new List<DataGridViewRow>();

        public int NestingLevel => ParentRows.Count;
        public bool HasChildItems => RowIndex + 1 < DataGridView.RowCount && ((WatchNameCell)DataGridView.Rows[RowIndex + 1].Cells[ColumnIndex]).ParentRows.Contains(OwningRow);

        private int ButtonSizeX => Size.Height - Size.Height % 2 - 4;
        private int NestedButtonExtentX => ButtonSizeX * (NestingLevel + 1);

        private bool? _listExpandedByUser;
        public bool ListExpanded { get => _listExpandedByUser ?? (NestingLevel == 0); set { _listExpandedByUser = value; ExpandCollapseChildren(); } }

        public WatchNameCell() : base() { }

        public void ExpandCollapse(bool parentExpanded)
        {
            if (OwningRow.Visible = parentExpanded)
                ResizeColumnToFitValue();
        }

        protected override object GetValue(int rowIndex)
        {
            return ParentRows.Count > 0 ? (ParentRows[ParentRows.Count - 1].Cells[ColumnIndex].Value + $"[{IndexInList}]") : base.GetValue(rowIndex);
        }

        protected override bool SetValue(int rowIndex, object value)
        {
            var valueSet = base.SetValue(rowIndex, value);
            if (valueSet && DataGridView != null)
                ResizeColumnToFitValue();
            return valueSet;
        }

        protected override void Paint(Graphics graphics,
            Rectangle clipBounds,
            Rectangle cellBounds,
            int rowIndex,
            DataGridViewElementStates cellState,
            object value,
            object formattedValue,
            string errorText,
            DataGridViewCellStyle cellStyle,
            DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            cellStyle.Padding = new Padding(NestedButtonExtentX, 0, 0, 0);
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

            if (HasChildItems)
            {
                var color = ((cellState & DataGridViewElementStates.Selected) != 0) ? Color.LightGray : Color.DimGray;
                var pen = new Pen(color, 2.0f);

                float sz = ButtonSizeX + (ButtonSizeX % 4) - 8; // must be a multiple of 4
                float x0 = cellBounds.X + (ButtonSizeX * NestingLevel) + 6, y0 = cellBounds.Y + (Size.Height - sz) / 2;

                if (ListExpanded)
                    graphics.DrawLines(pen, new[] { new PointF(x0, y0 + sz * 0.25f), new PointF(x0 + sz * 0.5f, y0 + sz * 0.75f), new PointF(x0 + sz, y0 + sz * 0.25f) });
                else
                    graphics.DrawLines(pen, new[] { new PointF(x0 + sz * 0.25f, y0), new PointF(x0 + sz * 0.75f, y0 + sz * 0.5f), new PointF(x0 + sz * 0.25f, y0 + sz) });
            }
        }

        protected override Size GetPreferredSize(Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize)
        {
            var textSize = base.GetPreferredSize(graphics, cellStyle, rowIndex, constraintSize);
            return new Size(textSize.Width + NestedButtonExtentX, textSize.Height);
        }

        public override Rectangle PositionEditingPanel(Rectangle cellBounds, Rectangle cellClip, DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            var pos = base.PositionEditingPanel(cellBounds, cellClip, cellStyle, singleVerticalBorderAdded, singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);
            return new Rectangle(pos.X + NestedButtonExtentX + 1, pos.Y, pos.Width - NestedButtonExtentX - 1, pos.Height);
        }

        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView.EditingControl is DataGridViewTextBoxEditingControl textBox)
            {
                // Defer SelectAll so that it runs after the default TextBox logic (which places the caret at the end)
                DataGridView.BeginInvoke((MethodInvoker)delegate
                {
                    textBox.SelectAll();
                });

                textBox.TextChanged += ResizeColumnToFitEditedValue;
            }
        }

        public override void DetachEditingControl()
        {
            if (DataGridView.EditingControl is DataGridViewTextBoxEditingControl textBox)
            {
                textBox.TextChanged -= ResizeColumnToFitEditedValue;
            }
            base.DetachEditingControl();
        }

        private void ResizeColumnToFitEditedValue(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            var requiredWidth = TextRenderer.MeasureText(textBox.Text, textBox.Font).Width + textBox.Margin.Horizontal + NestedButtonExtentX;
            if (DataGridView.Columns[ColumnIndex].Width < requiredWidth)
                DataGridView.Columns[ColumnIndex].Width = requiredWidth;
        }

        private void ResizeColumnToFitValue()
        {
            var requiredWidth = PreferredSize.Width;
            if (DataGridView.Columns[ColumnIndex].Width < requiredWidth)
                DataGridView.Columns[ColumnIndex].Width = requiredWidth;
        }

        protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
        {
            if (ExpanderButtonClicked(e))
                ListExpanded = !ListExpanded;
            else
                base.OnMouseClick(e);
        }

        protected override void OnMouseDoubleClick(DataGridViewCellMouseEventArgs e)
        {
            if (ExpanderButtonClicked(e))
            {
                ListExpanded = !ListExpanded;
            }
            else
            {
                base.OnMouseDoubleClick(e);
                if (NestingLevel == 0)
                    DataGridView.BeginEdit(false);
            }
        }

        private bool ExpanderButtonClicked(DataGridViewCellMouseEventArgs e) =>
            e.Button == MouseButtons.Left && e.X < NestedButtonExtentX && e.ColumnIndex == DataGridView.CurrentCellAddress.X && e.RowIndex == DataGridView.CurrentCellAddress.Y;

        private void ExpandCollapseChildren()
        {
            for (var row = RowIndex + 1; row < DataGridView.RowCount; ++row)
            {
                var rowNameCell = (WatchNameCell)DataGridView.Rows[row].Cells[ColumnIndex];
                if (!rowNameCell.ParentRows.Contains(OwningRow))
                    break;
                var allLevelsExpanded = rowNameCell.ParentRows.TrueForAll(r => ((WatchNameCell)r.Cells[ColumnIndex]).ListExpanded);
                rowNameCell.ExpandCollapse(allLevelsExpanded);
            }
        }
    }
}
