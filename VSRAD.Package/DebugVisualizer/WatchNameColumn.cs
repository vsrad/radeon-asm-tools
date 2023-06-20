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

        private bool _listExpanded;
        public bool ListExpanded { get => _listExpanded; set { _listExpanded = value; ApplyListExpansion(); } }

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

        private void ApplyListExpansion()
        {
            for (var row = RowIndex + 1; row < DataGridView.RowCount; ++row)
            {
                var rowNameCell = (WatchNameCell)DataGridView.Rows[row].Cells[ColumnIndex];
                if (!rowNameCell.ParentRows.Contains(OwningRow))
                    break;
                var allLevelsExpanded = rowNameCell.ParentRows.TrueForAll(r => ((WatchNameCell)r.Cells[ColumnIndex]).ListExpanded);
                DataGridView.Rows[row].Visible = allLevelsExpanded;
            }
        }
    }
}
