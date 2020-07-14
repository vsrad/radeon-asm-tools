using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed class WavemapView
    {
        private readonly int _groupSize;
        private readonly int _laneDataSize;
        private readonly int _groupCount;

        private readonly uint[] _data;

        public WavemapView(uint[] data, int groupSize, int laneDataSize, int groupCount)
        {
            _data = data;
            _groupSize = groupSize;
            _groupCount = groupCount;
            _laneDataSize = laneDataSize;
        }

        public bool IsActiveGroup(int groupIndex) => groupIndex < _groupCount;

        public bool GroupExecuted(int groupIndex)
        {
            var execMaskIndex = groupIndex * _groupSize * _laneDataSize + (8 * _laneDataSize); // execmask is in 8-9 lane of system watch
            return _data[execMaskIndex] != 0 || _data[execMaskIndex + _laneDataSize] != 0;
        }
    }
}
