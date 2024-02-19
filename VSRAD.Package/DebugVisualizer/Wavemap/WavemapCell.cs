using System;
using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public readonly struct WavemapCell : IEquatable<WavemapCell>
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

        public static bool operator ==(WavemapCell left, WavemapCell right) =>
            left.Equals(right);

        public static bool operator !=(WavemapCell left, WavemapCell right) =>
            !(left == right);

        public override bool Equals(object o) =>
            o is WavemapCell cell && Equals(cell);

        public bool Equals(WavemapCell o) =>
            WaveIndex == o.WaveIndex && GroupIndex == o.GroupIndex;

        public override int GetHashCode() =>
            (WaveIndex, GroupIndex).GetHashCode();
    }
}
