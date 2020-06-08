using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public class TableState
    {
        public int DataColumnOffset { get; private set; }
        public int PhantomColumnIndex { get; private set; }
        public IReadOnlyList<DataGridViewColumn> DataColumns { get; private set; }
        public readonly ColumnResizeController ResizeController;
        public int ColumnWidth;

        public TableState(int dataColumnOffset, int phantomColumnIndex, int columnWidth, IReadOnlyList<DataGridViewColumn> dataColumns, ColumnResizeController resizeController)
        {
            DataColumnOffset = dataColumnOffset;
            PhantomColumnIndex = phantomColumnIndex;
            ColumnWidth = columnWidth;
            DataColumns = dataColumns;
            ResizeController = resizeController;
        }

        public void IncrementPhantomColumnIndex() => PhantomColumnIndex++;
    }
}
