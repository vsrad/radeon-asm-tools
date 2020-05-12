using System;
using System.Collections;
using VSRAD.Package.Options;

namespace VSRAD.Package.DebugVisualizer
{
    [Flags]
    public enum ColumnStates
    {
        None = 0,
        Visible = 2,
        Inactive = 4,
        HasLaneSeparator = 8,
        HasHiddenColumnSeparator = 16
    }

    public sealed class ComputedColumnStyling
    {
        public uint GroupSize { get; private set; }
        public ColumnStates[] ColumnState { get; } = new ColumnStates[VisualizerTable.DataColumnCount];

        public void GrayOutColumns(uint groupSize)
        {
            GroupSize = groupSize;
            for (int i = 0; i < groupSize; i++)
                ColumnState[i] |= ColumnStates.Inactive;
        }

        public void Recompute(VisualizerOptions options, ColumnStylingOptions styling, uint groupSize, uint[] system)
        {
            GroupSize = groupSize;

            Array.Clear(ColumnState, 0, ColumnState.Length);
            foreach (int i in ColumnSelector.ToIndexes(styling.VisibleColumns))
                ColumnState[i] |= ColumnStates.Visible;

            ComputeInactiveLanes(options, system);
            ComputeLaneGrouping(options);
            ComputeHiddenColumnSeparators();
        }

        private void ComputeInactiveLanes(VisualizerOptions options, uint[] system)
        {
            if (system == null)
                return;
            for (int wfrontOffset = 0; wfrontOffset < GroupSize; wfrontOffset += 64)
            {
                if (options.CheckMagicNumber && system[wfrontOffset] != options.MagicNumber)
                {
                    for (int laneId = 0; laneId < 64; ++laneId)
                        ColumnState[wfrontOffset + laneId] |= ColumnStates.Inactive;
                }
                else if (options.MaskLanes)
                {
                    var execMask = new BitArray(new[] { (int)system[wfrontOffset + 8], (int)system[wfrontOffset + 9] });
                    for (int laneId = 0; laneId < 64; ++laneId)
                        if (!execMask[laneId])
                            ColumnState[wfrontOffset + laneId] |= ColumnStates.Inactive;
                }
            }
        }

        private void ComputeLaneGrouping(VisualizerOptions options)
        {
            var laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            if (laneGrouping == 0)
                return;
            for (uint start = 0; start < GroupSize - laneGrouping; start += laneGrouping)
            {
                for (int lastVisibleInGroup = (int)Math.Min(start + laneGrouping - 1, GroupSize - 1);
                    lastVisibleInGroup >= start; lastVisibleInGroup--)
                {
                    if ((ColumnState[lastVisibleInGroup] & ColumnStates.Visible) != 0)
                    {
                        ColumnState[lastVisibleInGroup] |= ColumnStates.HasLaneSeparator;
                        break;
                    }
                }
            }
        }

        private void ComputeHiddenColumnSeparators()
        {
            for (int i = 0; i < GroupSize - 1; i++)
                if ((ColumnState[i] & ColumnStates.Visible) != 0 && (ColumnState[i + 1] & ColumnStates.Visible) == 0)
                    ColumnState[i] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
