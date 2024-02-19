using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed class WavemapCell
    {
        public uint WaveIndex { get; }
        public uint GroupIndex { get; }
        public WaveStatus Wave { get; }

        public WavemapCell(uint waveIndex, uint groupIndex, WaveStatus wave)
        {
            WaveIndex = waveIndex;
            GroupIndex = groupIndex;
            Wave = wave;
        }
    }

}
