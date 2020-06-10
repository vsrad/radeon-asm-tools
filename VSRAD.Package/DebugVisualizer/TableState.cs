using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class TableState
    {
        public DataGridView Table { get; }
        public int DataColumnOffset { get; }
        public int PhantomColumnIndex => DataColumnOffset + DataColumns.Count;
        public List<DataGridViewColumn> DataColumns { get; }
        public ColumnResizeController ResizeController { get; }
        public int ColumnWidth { get; set; }

        public TableState(DataGridView table, int dataColumnOffset, int columnWidth, List<DataGridViewColumn> dataColumns)
        {
            Table = table;
            DataColumnOffset = dataColumnOffset;
            ColumnWidth = columnWidth;
            DataColumns = dataColumns;
            ResizeController = new ColumnResizeController(this);
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
