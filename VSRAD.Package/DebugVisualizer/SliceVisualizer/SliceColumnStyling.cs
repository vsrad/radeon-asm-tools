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
        public List<ColumnStates> ColumnStates { get; } = new List<ColumnStates>();
        public uint SubgroupSize { get; private set; }

        private readonly SliceVisualizerTable _table;

        public SliceColumnStyling(SliceVisualizerTable table)
        {
            _table = table;
        }
        
        public void GrayOutColumns() => ColumnStates.ForEach(s => s |= DebugVisualizer.ColumnStates.Inactive);

        public void Recompute(int subgroupSize, string columnSelector)
        {
            ColumnStates.Clear();
            ColumnStates.AddRange(new ColumnStates[subgroupSize]);
            foreach (var i in ColumnSelector.ToIndexes(columnSelector))
                ColumnStates[i] |= DebugVisualizer.ColumnStates.Visible;
            Apply(subgroupSize);
        }

        private void Apply(int subgroupSize)
        {
            for (int i = 0; i < _table.SelectedWatch.ColumnCount; i++)
                _table.Columns[i + SliceVisualizerTable.DataColumnOffset].Visible =
                    ((ColumnStates[i % subgroupSize] & DebugVisualizer.ColumnStates.Visible) != 0);
        }
    }
}
