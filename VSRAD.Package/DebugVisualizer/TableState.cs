using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public class TableState
    {
        public int DataColumnOffset { get; }
        public int PhantomColumnIndex => DataColumnOffset + DataColumns.Count;
        public List<DataGridViewColumn> DataColumns { get; }
        public ColumnResizeController ResizeController { get; }
        public int ColumnWidth { get; set; }

        public TableState(int dataColumnOffset, int columnWidth, List<DataGridViewColumn> dataColumns, ColumnResizeController resizeController)
        {
            DataColumnOffset = dataColumnOffset;
            ColumnWidth = columnWidth;
            DataColumns = dataColumns;
            ResizeController = resizeController;
        }
    }
}
