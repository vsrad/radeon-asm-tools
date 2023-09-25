using System.Collections.Generic;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed class WaveInfo
    {
        public Color BreakColor { get; }
        public uint? BreakLine { get; }
        public uint GroupIndex { get; }
        public uint WaveIndex { get; }
        public bool PartialExecMask { get; }

        public WaveInfo(Color breakColor, uint? breakLine, uint groupIndex, uint waveIndex, bool partialExecMask)
        {
            BreakColor = breakColor;
            BreakLine = breakLine;
            GroupIndex = groupIndex;
            WaveIndex = waveIndex;
            PartialExecMask = partialExecMask;
        }
    }

    public sealed class WavemapView
    {
        public delegate bool TryGetSystemData(uint groupIndex, uint waveIndex, out uint[] systemData);

        public static readonly Color Blue = Color.FromArgb(69, 115, 167);
        public static readonly Color Red = Color.FromArgb(172, 69, 70);
        public static readonly Color Green = Color.FromArgb(137, 166, 76);
        public static readonly Color Violet = Color.FromArgb(112, 89, 145);
        public static readonly Color Pink = Color.FromArgb(208, 147, 146);

        private readonly Dictionary<uint, Color> _breakpointColorMapping = new Dictionary<uint, Color>();
        private readonly Color[] _colors = new[] { WavemapView.Blue, WavemapView.Red, WavemapView.Green, WavemapView.Violet, WavemapView.Pink };
        private int _currentColorIndex = 0;

        private readonly TryGetSystemData _tryGetSystemData;

        public WavemapView(TryGetSystemData tryGetSystemData)
        {
            _tryGetSystemData = tryGetSystemData;
        }

        public WaveInfo GetWaveInfo(uint groupIndex, uint waveIndex, uint? checkMagicNumber, bool checkInactiveLanes)
        {
            if (!_tryGetSystemData(groupIndex, waveIndex, out var system))
                return null;

            uint? breakLine = system[Server.BreakStateData.SystemBreakLineLane];
            var magicNumber = system[Server.BreakStateData.SystemMagicNumberLane];
            if (checkMagicNumber is uint expectedMagicNumber && expectedMagicNumber != magicNumber)
                breakLine = null;

            var execLo = system[Server.BreakStateData.SystemExecLoLane];
            var execHi = system[Server.BreakStateData.SystemExecHiLane];
            var partialExecMask = checkInactiveLanes && (execLo != 0xffffffff || execHi != 0xffffffff);

            Color breakColor;
            if (breakLine is uint line)
            {
                if (!_breakpointColorMapping.TryGetValue(line, out breakColor))
                {
                    breakColor = _colors[_currentColorIndex];
                    _currentColorIndex = (_currentColorIndex + 1) % _colors.Length;
                    _breakpointColorMapping.Add(line, breakColor);
                }

                if (partialExecMask)
                    breakColor = Color.FromArgb(breakColor.R / 2, breakColor.G / 2, breakColor.B / 2);
            }
            else
            {
                breakColor = Color.Gray;
            }

            return new WaveInfo(breakColor: breakColor, breakLine: breakLine, groupIndex: groupIndex, waveIndex: waveIndex, partialExecMask: partialExecMask);
        }
    }
}
