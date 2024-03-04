using System;
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
        private ColumnStates[] _columnState = new ColumnStates[512];
        public ColumnStates[] ColumnState { get => _columnState; }

        public void Recompute(VisualizerOptions options, VisualizerAppearance appearance, ColumnStylingOptions styling, Server.BreakState breakState)
        {
            Array.Resize(ref _columnState, (int)breakState.GroupSize);
            Array.Clear(_columnState, 0, _columnState.Length);
            foreach (int i in ColumnSelector.ToIndexes(styling.VisibleColumns, (int)breakState.GroupSize))
                ColumnState[i] |= ColumnStates.Visible;

            ComputeInactiveLanes(options, breakState);
            ComputeLaneGrouping(appearance, breakState);
            ComputeHiddenColumnSeparators(breakState);
        }

        private void ComputeInactiveLanes(VisualizerOptions options, Server.BreakState breakState)
        {
            for (uint wave = 0, waveStartId = 0; wave < breakState.WavesPerGroup; wave += 1, waveStartId += breakState.Dispatch.WaveSize)
            {
                var waveStatus = breakState.GetWaveStatus(wave);
                var waveEndId = Math.Min(waveStartId + breakState.Dispatch.WaveSize, breakState.GroupSize);
                if (waveStatus.InstanceId == null)
                {
                    for (var lane = 0; lane + waveStartId < waveEndId; ++lane)
                        _columnState[lane + waveStartId] |= ColumnStates.Inactive;
                }
                else if (options.MaskLanes)
                {
                    for (var lane = 0; lane + waveStartId < waveEndId; ++lane)
                        if ((waveStatus.Exec & (1ul << lane)) == 0)
                            _columnState[lane + waveStartId] |= ColumnStates.Inactive;
                }
            }
        }

        private void ComputeLaneGrouping(VisualizerAppearance options, Server.BreakState breakState)
        {
            var laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            if (laneGrouping == 0 || laneGrouping > breakState.GroupSize)
                return;
            for (uint start = 0; start < breakState.GroupSize - laneGrouping; start += laneGrouping)
            {
                for (int lastVisibleInGroup = (int)Math.Min(start + laneGrouping - 1, breakState.GroupSize - 1);
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

        private void ComputeHiddenColumnSeparators(Server.BreakState breakState)
        {
            for (int i = 0; i < breakState.GroupSize - 1; i++)
                if ((_columnState[i] & ColumnStates.Visible) != 0 && (_columnState[i + 1] & ColumnStates.Visible) == 0)
                    _columnState[i] |= ColumnStates.HasHiddenColumnSeparator;
        }
    }
}
