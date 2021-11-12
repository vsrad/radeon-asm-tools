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
        public int minAllowedWidth { get; } = 30;
        public IReadOnlyList<DataGridViewColumn> DataColumns => _dataColumns;
        public int ColumnWidth { get; set; }
        public ScalingMode ScalingMode { get; set; } = ScalingMode.ResizeColumn;

        private readonly List<DataGridViewColumn> _dataColumns = new List<DataGridViewColumn>();

        // In some cases negative scrolling is required, but DataGridView doesn't support it.
        // We emulate such scrolling by adding paddin to first visible data column
        // and setting AdditionalScrollOffset to negative value
        private int AdditionalScrollOffset = 0;

        // In some cases we want to scroll beyond the last data column
        // We emulate such scrolling by adding inactive "phantom" column after all data columns
        // Note that phantom column index in Columns[] list is constant "1", but it always
        // displayed after all data columns
        public int PhantomColumnIndex { get; private set; } = -1;

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
            if (PhantomColumnIndex == -1)
            {
                PhantomColumnIndex = Table.Columns.Add(new DataGridViewTextBoxColumn
                {
                    FillWeight = 1,
                    MinimumWidth = 2,
                    Width = 2,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                DataColumnOffset = Table.Columns.Count;
            }
            _dataColumns.AddRange(columns);
            Table.Columns.AddRange(columns);
            Table.Columns[PhantomColumnIndex].DisplayIndex = Table.Columns.Count - 1;
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
            var n = DataColumns.Count(c => c.Visible);
            var phantomWidth = Math.Max(2, Table.Width - Table.Columns[0].Width - Table.TopLeftHeaderCell.Size.Width - w);
            TableShouldSuppressOnColumnWidthChangedEvent = true;

            foreach (var column in DataColumns)
                column.Width = w;

            if (firstVisible >= 0 && s < 0)
                Table.Columns[firstVisible].Width = w - s;
            if (PhantomColumnIndex >= 0)
                Table.Columns[PhantomColumnIndex].Width = phantomWidth;

            _invalidateCachedColumnsWidths();
            _performLayoutPrivate(false, false, false, false);
            Table.Invalidate();
            TableShouldSuppressOnColumnWidthChangedEvent = false;

            AdditionalScrollOffset = Math.Min(0, s);
            ColumnWidth = w;
            Table.HorizontalScrollingOffset = Math.Max(0, s);
        }

        public void ScaleNameColumn(int w)
        {
            Table.Columns[0].Width = w;
        }

        public int CountVisibleDataColumns(int index, bool include_current)
        {
            var n = DataColumns.Count(c => c.Visible && (c.Index < index || index == PhantomColumnIndex));
            return include_current ? n + 1 : n;
        }


        public int GetDataRegionWidth()
        {
            return Table.Size.Width - Table.RowHeadersWidth - Table.Columns[0].Width;
        }

        public float GetNormalizedXCoordinate(int x)
        {
            var DataRegionWidth = GetDataRegionWidth();
            var DataRegionMouseX = x - Table.RowHeadersWidth - Table.Columns[0].Width;

            return (float)DataRegionMouseX / DataRegionWidth;
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

            var n = CountVisibleDataColumns(clickedColumnIndex, false);
            var newScrollOffset = Math.Max(0, (n - 1) * (preferredWidth - ColumnWidth) + GetCurrentScroll());

            SetWidthAndScroll(preferredWidth, newScrollOffset);
        }
    }
}
