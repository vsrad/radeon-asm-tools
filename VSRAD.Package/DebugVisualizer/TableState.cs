using System.Collections.Generic;
using System.Windows.Forms;
using System;
using System.Reflection;
using System.Linq;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class TableState
    {
        public DataGridView Table { get; }
        public int DataColumnOffset { get; private set; } = 1;
        public int minAllowedWidth {get; } = 30;
        public IReadOnlyList<DataGridViewColumn> DataColumns => _dataColumns;
        public int ColumnWidth { get; set; }
        public ScalingMode ScalingMode { get; set; } = ScalingMode.ResizeColumn;

        private readonly List<DataGridViewColumn> _dataColumns = new List<DataGridViewColumn>();

        // In some cases negative scrolling is required, but DataGridView doesn't support it.
        // We emulate such scrolling by adding paddin to first visible data column
        // and setting AdditionalScrollOffset to negative value
        private int AdditionalScrollOffset = 0;

        public int GetCurrentScroll()
        {
            return Table.HorizontalScrollingOffset + AdditionalScrollOffset;
        }

        public TableState(DataGridView table, int columnWidth)
        {
            Table = table;
            ColumnWidth = columnWidth;

            // Out of the box DataGridView is unable to change the width of even several dozen columns in real time because it recalculates layout after each individual column is resized
            // (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,dc107b02a9e367cc)
            // We found that calling just these two methods at the end of scaling is enough to replicate that without performance issues:
            _invalidateCachedColumnsWidths = (InvalidateCachedColumnsWidths)Delegate.CreateDelegate(typeof(InvalidateCachedColumnsWidths), Table.Columns,
                typeof(DataGridViewColumnCollection).GetMethod("InvalidateCachedColumnsWidths", BindingFlags.NonPublic | BindingFlags.Instance));
            _performLayoutPrivate = (PerformLayoutPrivate)Delegate.CreateDelegate(typeof(PerformLayoutPrivate), Table,
                typeof(DataGridView).GetMethod("PerformLayoutPrivate", BindingFlags.NonPublic | BindingFlags.Instance));
        }

        public void AddDataColumns(DataGridViewColumn[] columns)
        {
            _dataColumns.AddRange(columns);
            Table.Columns.AddRange(columns);
        }

        public int GetFirstVisibleDataColumnIndex()
        {
            foreach (var column in DataColumns)
                if (column.Visible)
                    return column.Index;
            return -1;
        }

        public void Scroll(int diff, bool disablePaddingGrow)
        {
            int newScroll = GetCurrentScroll() + diff;
            if (AdditionalScrollOffset == 0 && newScroll >= 0)
            {
                Table.HorizontalScrollingOffset = newScroll;
            }
            else
            {
                if (disablePaddingGrow && newScroll < 0)
                {
                    newScroll = Math.Max(GetCurrentScroll(), newScroll);
                }
                SetWidthAndScroll(ColumnWidth, newScroll);
            }
        }

        public void RemoveScrollPadding()
        {
            if (AdditionalScrollOffset < 0)
                SetWidthAndScroll(ColumnWidth, Math.Max(0, GetCurrentScroll()));
        }

        public void SetWidthAndScroll(int w, int s)
        {
            var firstVisible = GetFirstVisibleDataColumnIndex();
            TableShouldSuppressOnColumnWidthChangedEvent = true;

            foreach (var column in DataColumns)
                column.Width = w;

            if (firstVisible >= 0 && s < 0)
                Table.Columns[firstVisible].Width = w - s;

            _invalidateCachedColumnsWidths();
            _performLayoutPrivate(false, false, false, false);
            Table.Invalidate();
            TableShouldSuppressOnColumnWidthChangedEvent = false;

            AdditionalScrollOffset = Math.Min(0, s);
            ColumnWidth = w;
            Table.HorizontalScrollingOffset = Math.Max(0, s);
        }

        public int CountVisibleColumns(int index, bool include_current)
        {
            var n = DataColumns.Count(c => c.Visible && c.Index < index);
            return include_current ? n + 1 : n;
        }






        public bool TableShouldSuppressOnColumnWidthChangedEvent { get; private set; }

        private delegate void InvalidateCachedColumnsWidths();
        private delegate void PerformLayoutPrivate(bool f1, bool f2, bool f3, bool f4);
        private readonly InvalidateCachedColumnsWidths _invalidateCachedColumnsWidths;
        private readonly PerformLayoutPrivate _performLayoutPrivate;


        public void FitWidth(int clickedColumnIndex)
        {
            int widestColumnIndex = 0;
            int maxApproxWidth = 0;

            var font = Table.DefaultCellStyle.Font;

            // DataGridViewColumn.GetPreferredWidth is expensive, so we call it only once for the widest column,
            // which is approximated by measuring text width for every cell
            foreach (var column in DataColumns)
            {
                if (!column.Visible)
                    continue;
                foreach (DataGridViewRow row in Table.Rows)
                {
                    var value = (string)row.Cells[column.Index].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var width = TextRenderer.MeasureText(value, font).Width + column.DividerWidth;
                        if (width > maxApproxWidth)
                        {
                            maxApproxWidth = width;
                            widestColumnIndex = column.Index;
                        }
                    }
                }
            }

            var preferredWidth = Table.Columns[widestColumnIndex].GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            preferredWidth = Math.Max(preferredWidth, minAllowedWidth);

            var n = CountVisibleColumns(clickedColumnIndex, false);
            var newScrollOffset = Math.Max(0, (n - 1) * (preferredWidth - ColumnWidth) + GetCurrentScroll());

            SetWidthAndScroll(preferredWidth, newScrollOffset);
        }
    }
}
