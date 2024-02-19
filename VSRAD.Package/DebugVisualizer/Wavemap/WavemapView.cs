using System.Collections.Generic;
using System.Drawing;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed class WaveInfo
    {
        public Color BreakColor { get; }
        public BreakpointInfo Breakpoint { get; }
        public uint GroupIndex { get; }
        public uint WaveIndex { get; }
        public bool PartialExecMask { get; }

        public WaveInfo(Color breakColor, BreakpointInfo breakpoint, uint groupIndex, uint waveIndex, bool partialExecMask)
        {
            BreakColor = breakColor;
            Breakpoint = breakpoint;
            GroupIndex = groupIndex;
            WaveIndex = waveIndex;
            PartialExecMask = partialExecMask;
        }
    }

    public sealed class WavemapView
    {
        public delegate bool TryGetGlobalWaveMeta(uint groupIndex, uint waveIndex, out uint breakpointIdx, out BreakpointInfo breakpoint, out ulong execMask);

        public static readonly Color Blue = Color.FromArgb(69, 115, 167);
        public static readonly Color Red = Color.FromArgb(172, 69, 70);
        public static readonly Color Green = Color.FromArgb(137, 166, 76);
        public static readonly Color Violet = Color.FromArgb(112, 89, 145);
        public static readonly Color Pink = Color.FromArgb(208, 147, 146);

        private readonly Dictionary<uint, Color> _breakpointColorMapping = new Dictionary<uint, Color>();
        private readonly Color[] _colors = new[] { WavemapView.Blue, WavemapView.Red, WavemapView.Green, WavemapView.Violet, WavemapView.Pink };
        private int _currentColorIndex = 0;

        private readonly TryGetGlobalWaveMeta _tryGetGlobalWaveMeta;

        public WavemapView(TryGetGlobalWaveMeta tryGetGlobalWaveMeta)
        {
            _tryGetGlobalWaveMeta = tryGetGlobalWaveMeta;
        }

        public WaveInfo GetWaveInfo(uint groupIndex, uint waveIndex, bool checkInactiveLanes)
        {
            if (!_tryGetGlobalWaveMeta(groupIndex, waveIndex, out var breakpointIdx, out var breakpoint, out var execMask))
                return null;

            var (breakColor, partialExecMask) = (Color.Gray, false);
            if (breakpoint != null)
            {
                if (!_breakpointColorMapping.TryGetValue(breakpointIdx, out breakColor))
                {
                    breakColor = _colors[_currentColorIndex];
                    _currentColorIndex = (_currentColorIndex + 1) % _colors.Length;
                    _breakpointColorMapping.Add(breakpointIdx, breakColor);
                }

                partialExecMask = checkInactiveLanes && (execMask != 0xffffffff_ffffffff);
                if (partialExecMask)
                    breakColor = Color.FromArgb(breakColor.R / 2, breakColor.G / 2, breakColor.B / 2);
            }
            return new WaveInfo(breakColor, breakpoint, groupIndex: groupIndex, waveIndex: waveIndex, partialExecMask: partialExecMask);
        }
    }
}
