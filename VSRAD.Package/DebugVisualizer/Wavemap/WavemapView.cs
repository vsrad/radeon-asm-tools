using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    struct WaveInfo
    {
        Color BreakColor;
        uint BreakLine;
        uint GroupIdx;
        uint WaveIdx;
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

        private readonly uint[] _data;

        private readonly BreakpointColorManager _colorManager;

        public WavemapView(uint[] data, int waveSize, int laneDataSize)
        {
            _data = data;
            _waveSize = waveSize;
            _laneDataSize = laneDataSize;
            _colorManager = new BreakpointColorManager();
        }

        public uint GetBreakpointLine(int waveIndex)
        {
            var breakIndex = waveIndex * _waveSize * _laneDataSize + _laneDataSize; // break line is in the first lane of system watch
            return _data[breakIndex];
        }

        public Color GetWaveColor(int waveIndex)
        {
            return _colorManager.GetColorForBreakpoint(GetBreakpointLine(waveIndex));
        }
    }
}
