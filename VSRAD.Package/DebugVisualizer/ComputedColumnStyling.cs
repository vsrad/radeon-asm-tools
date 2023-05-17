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
        private ColumnStates[] _columnState = new ColumnStates[512];
        public ColumnStates[] ColumnState { get => _columnState; }

        private uint _groupSize;
        private uint _waveSize;

        public void Recompute(VisualizerOptions options, VisualizerAppearance appearance, ColumnStylingOptions styling, uint groupSize, uint waveSize, Server.BreakStateData breakData)
        {
            _groupSize = groupSize;
            _waveSize = waveSize;

            Array.Resize(ref _columnState, (int)groupSize);
            Array.Clear(_columnState, 0, _columnState.Length);
            foreach (int i in ColumnSelector.ToIndexes(styling.VisibleColumns, (int)groupSize))
                ColumnState[i] |= ColumnStates.Visible;

            ComputeInactiveLanes(options, breakData);
            ComputeLaneGrouping(appearance);
            ComputeHiddenColumnSeparators();
        }

        private void ComputeInactiveLanes(VisualizerOptions options, Server.BreakStateData breakData)
        {
            if (breakData != null && breakData.WaveSize == _waveSize && breakData.GroupSize == _groupSize)
            {
                foreach (var waveView in breakData.GetWaveViews())
                {
                    var waveEnd = Math.Min(waveView.StartThreadId + waveView.WaveSize, breakData.GroupSize);
                    if (options.CheckMagicNumber && waveView.GetSystemMagicNumber() != options.MagicNumber)
                    {
                        for (var lane = 0; lane + waveView.StartThreadId < waveEnd; ++lane)
                            _columnState[lane + waveView.StartThreadId] |= ColumnStates.Inactive;
                    }
                    else if (options.MaskLanes)
                    {
                        var execMaskDwords = waveView.GetSystemExecMask();
                        var execMask = new BitArray(new[] { (int)execMaskDwords[0], (int)execMaskDwords[1] });
                        for (var lane = 0; lane + waveView.StartThreadId < waveEnd; ++lane)
                            if (!execMask[lane])
                                _columnState[lane + waveView.StartThreadId] |= ColumnStates.Inactive;
                    }
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
