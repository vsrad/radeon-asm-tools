using System.Collections.Generic;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public struct WaveInfo
    {
        public Color BreakColor;
        public uint BreakLine;
        public int GroupIdx;
        public int WaveIdx;
    }

    class BreakpointColorManager
    {
        private readonly Dictionary<uint, Color> _breakpointColorMapping = new Dictionary<uint, Color>();
        private readonly Color[] _colors = new Color[] { Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Cyan };
        private uint _currentColorIndex;

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
        private readonly int _waveSize;
        private readonly int _laneDataSize;
        private readonly int _wavesPerGroup;

        private readonly uint[] _data;

        private readonly BreakpointColorManager _colorManager;

        public WavemapView(uint[] data, int waveSize, int laneDataSize, int groupSize)
        {
            _data = data;
            _waveSize = waveSize;
            _laneDataSize = laneDataSize;
            _wavesPerGroup = groupSize / waveSize;
            _colorManager = new BreakpointColorManager();
        }

        private uint GetBreakpointLine(int waveIndex)
        {
            var breakIndex = waveIndex * _waveSize * _laneDataSize + _laneDataSize; // break line is in the first lane of system watch
            return _data[breakIndex];
        }

        private int GetWaveFlatIndex(int row, int column) => column * _wavesPerGroup + row;

        private WaveInfo GetWaveInfoByRowAndColumn(int row, int column)
        {
            var flatIndex = GetWaveFlatIndex(row, column);
            var breakLine = GetBreakpointLine(flatIndex);

            return new WaveInfo
            {
                BreakColor = _colorManager.GetColorForBreakpoint(breakLine),
                BreakLine = breakLine,
                GroupIdx = column,
                WaveIdx = row
            };
        }

        public WaveInfo this[int row, int column]
        {
            get => GetWaveInfoByRowAndColumn(row, column);
        }
    }
}
