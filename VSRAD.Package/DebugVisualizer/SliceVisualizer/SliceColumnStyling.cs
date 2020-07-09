using System.Collections.Generic;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceColumnStyling
    {
        public List<ColumnStates> ColumnState { get; } = new List<ColumnStates>();
        public uint SubgroupSize { get; private set; }

        private readonly SliceVisualizerTable _table;
        private readonly Options.VisualizerAppearance _appearance;

        public SliceColumnStyling(SliceVisualizerTable table, Options.VisualizerAppearance appearance)
        {
            _table = table;
            _appearance = appearance;
        }

        public ColumnStates this[int index]
        {
            get {
                if (SubgroupSize == 0) return default;
                var i = index % (int)SubgroupSize;
                if (i >= ColumnState.Count) return default;
                return ColumnState[i];
            }
        }
        
        public void GrayOutColumns() => ColumnState.ForEach(s => s |= ColumnStates.Inactive);

        public void Recompute(int subgroupSize, string columnSelector, Options.VisualizerAppearance appearance)
        {
            if (subgroupSize == 0) return;
            SubgroupSize = (uint)subgroupSize;
            ColumnState.Clear();
            ColumnState.AddRange(new ColumnStates[subgroupSize]);
            foreach (var i in ColumnSelector.ToIndexes(columnSelector, (int)_table.GroupSize))
                if (i < subgroupSize)
                    ColumnState[i] |= ColumnStates.Visible;

            ComputeHiddenColumnSeparators(subgroupSize);
            ColumnState[subgroupSize - 1] |= ColumnStates.HasSubgroupSeparator;
            if (_table.SelectedWatch == null) return;
            Apply(subgroupSize);
        }

        private void Apply(int subgroupSize)
        {
            for (int i = 0; i < _table.SelectedWatch.ColumnCount; i++)
            {
                _table.Columns[i + SliceVisualizerTable.DataColumnOffset].Visible =
                    (ColumnState[i % subgroupSize] & ColumnStates.Visible) != 0;
                if ((ColumnState[i % subgroupSize] & ColumnStates.HasHiddenColumnSeparator) != 0)
                    _table.Columns[i + SliceVisualizerTable.DataColumnOffset].DividerWidth = _appearance.SliceHiddenColumnSeparatorWidth;
                else if ((ColumnState[i % subgroupSize] & ColumnStates.HasSubgroupSeparator) != 0)
                    _table.Columns[i + SliceVisualizerTable.DataColumnOffset].DividerWidth = _appearance.SliceSubgroupSeparatorWidth;
                else
                    _table.Columns[i + SliceVisualizerTable.DataColumnOffset].DividerWidth = 0;
            }
        }

        private void ComputeHiddenColumnSeparators(int subgroupSize)
        {
            for (int i = 0; i < subgroupSize - 1; i++)
                if ((ColumnState[i] & ColumnStates.Visible) != 0 && (ColumnState[i + 1] & ColumnStates.Visible) == 0)
                    ColumnState[i] |= ColumnStates.HasHiddenColumnSeparator;
            if ((ColumnState[subgroupSize - 1] & ColumnStates.Visible) != 0 && (ColumnState[0] & ColumnStates.Visible) == 0)
                ColumnState[subgroupSize - 1] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
