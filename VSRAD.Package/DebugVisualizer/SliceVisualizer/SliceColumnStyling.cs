using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceColumnStyling
    {
        public List<ColumnStates> ColumnState { get; } = new List<ColumnStates>();
        public uint SubgroupSize { get; private set; }

        private readonly SliceVisualizerTable _table;

        public SliceColumnStyling(SliceVisualizerTable table)
        {
            _table = table;
        }
        
        public void GrayOutColumns() => ColumnState.ForEach(s => s |= DebugVisualizer.ColumnStates.Inactive);

        public void Recompute(int subgroupSize, string columnSelector)
        {
            ColumnState.Clear();
            ColumnState.AddRange(new ColumnStates[subgroupSize]);
            foreach (var i in ColumnSelector.ToIndexes(columnSelector))
                ColumnState[i] |= ColumnStates.Visible;
            ComputeHiddenColumnSeparators(subgroupSize);
            Apply(subgroupSize);
        }

        private void Apply(int subgroupSize)
        {
            for (int i = 0; i < _table.SelectedWatch.ColumnCount; i++)
            {
                _table.Columns[i + SliceVisualizerTable.DataColumnOffset].Visible =
                    (ColumnState[i % subgroupSize] & ColumnStates.Visible) != 0;
                if ((ColumnState[i % subgroupSize] & ColumnStates.HasHiddenColumnSeparator) != 0)
                    _table.Columns[i + SliceVisualizerTable.DataColumnOffset].DividerWidth = 8;//_hiddenColumnSeparatorWidth;
            }
        }

        private void ComputeHiddenColumnSeparators(int subgroupSize)
        {
            for (int i = 0; i < subgroupSize; i++)
                if ((ColumnState[i] & ColumnStates.Visible) != 0 && (ColumnState[i + 1] & ColumnStates.Visible) == 0)
                    ColumnState[i] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
