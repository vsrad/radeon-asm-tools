using System.Collections.Generic;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
#pragma warning disable CA1815 // the comparing of this structs is not a case, so disable warning that tells us to implement Equals()
    public struct WaveInfo
    {
        public Color BreakColor;
        public uint BreakLine;
        public uint GroupIdx;
        public uint WaveIdx;
        public bool IsVisible;
        public bool PartialMask;
        public bool BreakNotReached;
    }
#pragma warning restore CA1815

    class BreakpointColorManager
    {
        private readonly Dictionary<uint, Color> _breakpointColorMapping = new Dictionary<uint, Color>();
        private readonly Color[] _colors = new Color[] { WavemapView.Blue, WavemapView.Red, WavemapView.Green, WavemapView.Violet, WavemapView.Pink };
        private int _currentColorIndex;

        private Color GetNextColor()
        {
            if (_currentColorIndex == _colors.Length) _currentColorIndex = 0;
            return _colors[_currentColorIndex++];
        }

        public Color GetColorForBreakpoint(uint breakLine)
        {
            if (_breakpointColorMapping.TryGetValue(breakLine, out var color))
            {
                return color;
            }
            else
            {
                var c = GetNextColor();
                _breakpointColorMapping.Add(breakLine, c);
                return c;
            }
        }
    }

    public sealed class WavemapView
    {
        public static readonly Color Blue = Color.FromArgb(69, 115, 167);
        public static readonly Color Red = Color.FromArgb(172, 69, 70);
        public static readonly Color Green = Color.FromArgb(137, 166, 76);
        public static readonly Color Violet = Color.FromArgb(112, 89, 145);
        public static readonly Color Pink = Color.FromArgb(208, 147, 146);

        private readonly int _waveSize;
        private readonly int _laneDataSize;
        public int WavesPerGroup { get; }
        public int GroupCount { get; }

        public bool CheckInactiveLanes = false;
        public bool CheckMagicNumber = false;
        public uint MagicNumber = 0;

        private readonly uint[] _data;

        private readonly BreakpointColorManager _colorManager;

        public WavemapView(uint[] data, int waveSize, int laneDataSize, int groupSize, int groupCount)
        {
            _data = data;
            _waveSize = waveSize;
            _laneDataSize = laneDataSize;
            WavesPerGroup = (groupSize + waveSize - 1) / waveSize;
            GroupCount = groupCount;
            _colorManager = new BreakpointColorManager();
        }

        private uint? GetBreakpointLine(int waveIndex)
        {
            var breakIndex = waveIndex * _waveSize * _laneDataSize + _laneDataSize; // break line is in the lane #1 of system watch
            if (breakIndex >= _data.Length) // last wave, last group, wave size == 1
                return null;
            return _data[breakIndex];
        }

        private bool IsValidWave(int waveIdx, int groupIdx) => waveIdx < WavesPerGroup && groupIdx < GroupCount;

        private bool HasInactiveLanes(int flatWaveIndex)
        {
            var execLoOffset = flatWaveIndex * _waveSize * _laneDataSize + (_laneDataSize * 8); // exec mask is in the lanes #8 and #9 of system watch
            var execHiOffset = execLoOffset + _laneDataSize;
            return _waveSize >= 10 && (_data[execLoOffset] != 0xffffffff || _data[execHiOffset] != 0xffffffff);
        }

        private bool MagicNumberSet(int flatWaveIndex)
        {
            var magicNumberOffset = flatWaveIndex * _waveSize * _laneDataSize; // magic number is in the lane #0 of system watch
            return _data[magicNumberOffset] == MagicNumber;
        }

        public WaveInfo this[int waveIdx, int groupIdx]
        {
            get
            {
                var flatIndex = groupIdx * WavesPerGroup + waveIdx;
                if (IsValidWave(waveIdx, groupIdx) && GetBreakpointLine(flatIndex) is uint breakLine)
                {
                    var breakNotReached = CheckMagicNumber && !MagicNumberSet(flatIndex);
                    var partialMask = CheckInactiveLanes && HasInactiveLanes(flatIndex);

                    Color breakColor;
                    if (breakNotReached)
                        breakColor = Color.Gray;
                    else if (partialMask)
                        breakColor = Color.LightGray;
                    else
                        breakColor = _colorManager.GetColorForBreakpoint(breakLine);

                    return new WaveInfo
                    {
                        BreakColor = breakColor,
                        BreakLine = breakLine,
                        GroupIdx = (uint)groupIdx,
                        WaveIdx = (uint)waveIdx,
                        IsVisible = true,
                        PartialMask = partialMask,
                        BreakNotReached = breakNotReached
                    };
                }
                else
                {
                    return default;
                }
            }
        }
    }
}
