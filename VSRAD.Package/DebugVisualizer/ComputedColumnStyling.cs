using System;
using System.Collections;
using System.Linq;
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
        HasHiddenColumnSeparator = 16,
        HasSubgroupSeparator = 32
    }

    public sealed class ComputedColumnStyling
    {
        private ColumnStates[] _columnState = new ColumnStates[512];
        public ColumnStates[] ColumnState { get => _columnState; }

        private uint _groupSize;
        private uint _waveSize;

        public void Recompute(VisualizerOptions options, VisualizerAppearance appearance, ColumnStylingOptions styling, uint groupSize, uint waveSize, Server.WatchView system)
        {
            _groupSize = groupSize;
            _waveSize = waveSize;

            Array.Resize(ref _columnState, (int)groupSize);
            Array.Clear(_columnState, 0, _columnState.Length);
            foreach (int i in ColumnSelector.ToIndexes(styling.VisibleColumns, (int)groupSize))
                ColumnState[i] |= ColumnStates.Visible;

            ComputeInactiveLanes(options, system);
            ComputeLaneGrouping(appearance);
            ComputeHiddenColumnSeparators();
        }

        private void ComputeInactiveLanes(VisualizerOptions options, Server.WatchView system)
        {
            if (system == null)
                return;

            var lastItemId = Math.Min(_groupSize - 1, system.LastIndexInGroup);
            for (int waveOffset = 0; waveOffset < system.LastIndexInGroup; waveOffset += (int)_waveSize)
            {
                var lastLaneId = Math.Min(waveOffset + _waveSize - 1, lastItemId);
                if (options.CheckMagicNumber && system[waveOffset] != options.MagicNumber)
                {
                    for (int laneId = 0; waveOffset + laneId <= lastLaneId; ++laneId)
                        _columnState[waveOffset + laneId] |= ColumnStates.Inactive;
                }
                else if (options.MaskLanes && _waveSize <= 64 && waveOffset + 9 <= lastLaneId)
                {
                    var execMask = new BitArray(new[] { (int)system[waveOffset + 8], (int)system[waveOffset + 9] });
                    for (int laneId = 0; waveOffset + laneId <= lastLaneId; ++laneId)
                        if (!execMask[laneId])
                            _columnState[waveOffset + laneId] |= ColumnStates.Inactive;
                }
            }
        }

        private void ComputeLaneGrouping(VisualizerAppearance options)
        {
            var laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            if (laneGrouping == 0 || laneGrouping > _groupSize)
                return;
            for (uint start = 0; start < _groupSize - laneGrouping; start += laneGrouping)
            {
                for (int lastVisibleInGroup = (int)Math.Min(start + laneGrouping - 1, _groupSize - 1);
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
            for (int i = 0; i < _groupSize - 1; i++)
                if ((_columnState[i] & ColumnStates.Visible) != 0 && (_columnState[i + 1] & ColumnStates.Visible) == 0)
                    _columnState[i] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
