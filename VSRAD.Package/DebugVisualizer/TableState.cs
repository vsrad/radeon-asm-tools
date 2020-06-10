using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class TableState
    {
        public DataGridView Table { get; }
        public int DataColumnOffset { get; private set; } = -1;
        // A "phantom" column is inserted before the first data column. It is usually hidden, but becomes displayed
        // after the last visible column when the scale operation results in an (unwanted) horizontal offset change.
        public int PhantomColumnIndex { get; private set; } = -1;
        public IReadOnlyList<DataGridViewColumn> DataColumns => _dataColumns;
        public ColumnResizeController ResizeController { get; }
        public int ColumnWidth { get; set; }

        private readonly List<DataGridViewColumn> _dataColumns = new List<DataGridViewColumn>();

        public TableState(DataGridView table, int columnWidth)
        {
            Table = table;
            ColumnWidth = columnWidth;
            ResizeController = new ColumnResizeController(this);
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

        public int GetLastVisibleDataColumnIndex()
        {
            for (int i = DataColumns.Count - 1; i >= 0; --i)
                if (DataColumns[i].Visible)
                    return DataColumns[i].Index;
            return -1;
        }
    }
}
