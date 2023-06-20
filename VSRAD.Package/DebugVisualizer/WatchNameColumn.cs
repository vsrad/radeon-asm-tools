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
        public List<int> ParentRowIndexes { get; } = new List<int>();

        private bool _listExpanded;
        public bool ListExpanded { get => _listExpanded; set { _listExpanded = value; ApplyListExpansion(); } }

        private bool HasChildItems => RowIndex + 1 < DataGridView.RowCount && ((WatchNameCell)DataGridView.Rows[RowIndex + 1].Cells[ColumnIndex]).ParentRowIndexes.Contains(RowIndex);
        private int NestingLevel => ParentRowIndexes.Count;
        private int ButtonSizeX => Size.Height - Size.Height % 2 - 8;
        private int NestedButtonExtentX => ButtonSizeX * (NestingLevel + 1);

        public WatchNameCell() : base() { }

        protected override object GetValue(int rowIndex)
        {
            return IndexInList != -1 ? $"[{IndexInList}]" : base.GetValue(rowIndex);
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
                int rectSize = ButtonSizeX - 6, rectPadLeft = 4, rectPadTop = (Size.Height - rectSize) / 2;
                var btnRect = new Rectangle(cellBounds.X + (ButtonSizeX * NestingLevel) + rectPadLeft, cellBounds.Y + rectPadTop, rectSize, rectSize);

                var color = ((cellState & DataGridViewElementStates.Selected) != 0) ? Color.LightGray : Color.DimGray;
                var pen = new Pen(color, 1.0f);

                graphics.DrawRectangle(pen, btnRect);
                graphics.DrawLine(pen, btnRect.Left + 2, btnRect.Top + btnRect.Height / 2, btnRect.Right - 2, btnRect.Top + btnRect.Height / 2);
                if (!ListExpanded)
                    graphics.DrawLine(pen, btnRect.Left + btnRect.Width / 2, btnRect.Top + 2, btnRect.Left + btnRect.Width / 2, btnRect.Bottom - 2);
            }
        }

        public override Rectangle PositionEditingPanel(Rectangle cellBounds, Rectangle cellClip, DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            var pos = base.PositionEditingPanel(cellBounds, cellClip, cellStyle, singleVerticalBorderAdded, singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);
            return new Rectangle(pos.X + NestedButtonExtentX + 1, pos.Y, pos.Width - NestedButtonExtentX - 1, pos.Height);
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
            base.OnMouseDoubleClick(e);
            if (!ExpanderButtonClicked(e) && NestingLevel == 0)
                DataGridView.BeginEdit(false);
        }

        private bool ExpanderButtonClicked(DataGridViewCellMouseEventArgs e) =>
            e.Button == MouseButtons.Left && e.X < NestedButtonExtentX && e.ColumnIndex == DataGridView.CurrentCellAddress.X && e.RowIndex == DataGridView.CurrentCellAddress.Y;

        private void ApplyListExpansion()
        {
            for (var row = RowIndex + 1; row < DataGridView.RowCount; ++row)
            {
                var rowNameCell = (WatchNameCell)DataGridView.Rows[row].Cells[ColumnIndex];
                if (!rowNameCell.ParentRowIndexes.Contains(RowIndex))
                    break;
                var allLevelsExpanded = rowNameCell.ParentRowIndexes.TrueForAll(r => ((WatchNameCell)DataGridView.Rows[r].Cells[ColumnIndex]).ListExpanded);
                DataGridView.Rows[row].Visible = allLevelsExpanded;
            }
        }
    }
}
