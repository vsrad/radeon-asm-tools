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
        public delegate IReadOnlyList<BreakpointInfo> GetBreakpointList();
        public delegate bool TryGetSystemData(uint groupIndex, uint waveIndex, out uint magicNumber, out uint breakpointId, out ulong execMask);

        public static readonly Color Blue = Color.FromArgb(69, 115, 167);
        public static readonly Color Red = Color.FromArgb(172, 69, 70);
        public static readonly Color Green = Color.FromArgb(137, 166, 76);
        public static readonly Color Violet = Color.FromArgb(112, 89, 145);
        public static readonly Color Pink = Color.FromArgb(208, 147, 146);

        private readonly Dictionary<uint, Color> _breakpointColorMapping = new Dictionary<uint, Color>();
        private readonly Color[] _colors = new[] { WavemapView.Blue, WavemapView.Red, WavemapView.Green, WavemapView.Violet, WavemapView.Pink };
        private int _currentColorIndex = 0;

        private readonly GetBreakpointList _getBreakpointList;
        private readonly TryGetSystemData _tryGetSystemData;

        public WavemapView(GetBreakpointList getBreakpointList, TryGetSystemData tryGetSystemData)
        {
            _getBreakpointList = getBreakpointList;
            _tryGetSystemData = tryGetSystemData;
        }

        public WaveInfo GetWaveInfo(uint groupIndex, uint waveIndex, uint? checkMagicNumber, bool checkInactiveLanes)
        {
            if (!_tryGetSystemData(groupIndex, waveIndex, out var magicNumber, out var breakpointId, out var execMask))
                return null;

            var breakpointList = _getBreakpointList();
            var breakpointValid = breakpointId < breakpointList.Count;
            if (checkMagicNumber is uint expectedMagicNumber)
                breakpointValid = breakpointValid && expectedMagicNumber == magicNumber;

            var partialExecMask = checkInactiveLanes && (execMask != 0xffffffff_ffffffff);

            Color breakColor;
            if (breakpointValid)
            {
                if (!_breakpointColorMapping.TryGetValue(breakpointId, out breakColor))
                {
                    breakColor = _colors[_currentColorIndex];
                    _currentColorIndex = (_currentColorIndex + 1) % _colors.Length;
                    _breakpointColorMapping.Add(breakpointId, breakColor);
                }

                if (partialExecMask)
                    breakColor = Color.FromArgb(breakColor.R / 2, breakColor.G / 2, breakColor.B / 2);
            }
            else
            {
                breakColor = Color.Gray;
            }

            return new WaveInfo(breakColor: breakColor,
                breakpoint: breakpointValid ? breakpointList[(int)breakpointId] : null,
                groupIndex: groupIndex,
                waveIndex: waveIndex,
                partialExecMask: partialExecMask);
        }
    }
}
