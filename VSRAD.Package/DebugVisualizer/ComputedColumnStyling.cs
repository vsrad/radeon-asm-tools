using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Documents;
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

        private ColumnStates[] _columnState = new ColumnStates[512];
        public ColumnStates[] ColumnState { get => _columnState; }

        public void Recompute(VisualizerOptions options, ColumnStylingOptions styling, uint groupSize, Server.WatchView system)
        {
            GroupSize = groupSize;

            Array.Resize(ref _columnState, (int)groupSize);
            Array.Clear(_columnState, 0, _columnState.Length);
            foreach (int i in ColumnSelector.ToIndexes(styling.VisibleColumns, (int)groupSize))
                ColumnState[i] |= ColumnStates.Visible;

            ComputeInactiveLanes(options, system);
            ComputeLaneGrouping(options);
            ComputeHiddenColumnSeparators();
        }

        private void ComputeInactiveLanes(VisualizerOptions options, Server.WatchView system)
        {
            if (system == null)
                return;

            var wfrontSize = Math.Min(options.WaveSize, GroupSize);
            for (int wfrontOffset = 0; wfrontOffset < GroupSize; wfrontOffset += (int)wfrontSize)
            {
                var lastLaneId = Math.Min(wfrontOffset + wfrontSize, GroupSize) - 1; // handle incomplete groups (group size % wave size != 0)
                if (options.CheckMagicNumber && system[wfrontOffset] != options.MagicNumber)
                {
                    for (int laneId = 0; wfrontOffset + laneId <= lastLaneId; ++laneId)
                        _columnState[wfrontOffset + laneId] |= ColumnStates.Inactive;
                }
                else if (options.MaskLanes && wfrontSize <= 64 && wfrontOffset + 9 <= lastLaneId)
                {
                    var execMask = new BitArray(new[] { (int)system[wfrontOffset + 8], (int)system[wfrontOffset + 9] });
                    for (int laneId = 0; wfrontOffset + laneId <= lastLaneId; ++laneId)
                        if (!execMask[laneId])
                            _columnState[wfrontOffset + laneId] |= ColumnStates.Inactive;
                }
            }
        }

        private void ComputeLaneGrouping(VisualizerOptions options)
        {
            var laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            if (laneGrouping == 0 || laneGrouping > GroupSize)
                return;
            for (uint start = 0; start < GroupSize - laneGrouping; start += laneGrouping)
            {
                for (int lastVisibleInGroup = (int)Math.Min(start + laneGrouping - 1, GroupSize - 1);
                    lastVisibleInGroup >= start; lastVisibleInGroup--)
                {
                    if ((_columnState[lastVisibleInGroup] & ColumnStates.Visible) != 0)
                    {
                        _columnState[lastVisibleInGroup] |= ColumnStates.HasLaneSeparator;
                        break;
                    }
                }
            }
        }

        private void ComputeHiddenColumnSeparators()
        {
            for (int i = 0; i < GroupSize - 1; i++)
                if ((_columnState[i] & ColumnStates.Visible) != 0 && (_columnState[i + 1] & ColumnStates.Visible) == 0)
                    _columnState[i] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
