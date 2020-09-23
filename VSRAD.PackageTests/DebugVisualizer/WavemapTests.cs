using System.Drawing;
using System.Xml.Serialization;
using VSRAD.Package.DebugVisualizer.Wavemap;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class WavemapTests
    {
        /*
         * assuning data parameters is:
         * * wave size = 6
         * * group size = 12
         * * watches count = 2
         * * group count = 10
         * data size = (watches_count + 1[system]) * group_size * group_count
         */
        private readonly uint[] _data = new uint[360];

        [Fact]
        public void BreakLineTest()
        {
            for (uint i = 3, j = 313; i < 360; i += 18, j += 313)
                _data[i] = j;

            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3);

            for (uint i = 0, expected = 313; i < 20; ++i, expected += 313)
                Assert.Equal(expected, wavemapView.GetBreakpointLine((int)i));
        }

        [Fact]
        public void ColorAssignTest()
        {
            // assume that all waves hitted the same breakpoint
            for (uint i = 3, j = 313; i < 360; i += 18)
                _data[i] = j;

            /* Red, Blue, Green, Yellow, Cyan */
            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3);

            for (int i = 0; i < 20; ++i)
                Assert.Equal(Color.Red, wavemapView.GetWaveColor(i));

            // now lets assume that all waves hitted unique breakpoint
            for (uint i = 3, j = 313; i < 360; i += 18, j += 313)
                _data[i] = j;

            wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3);

            for (int i = 0; i < 20; i += 5)
            {
                Assert.Equal(Color.Red, wavemapView.GetWaveColor(i));
                Assert.Equal(Color.Blue, wavemapView.GetWaveColor(i+1));
                Assert.Equal(Color.Green, wavemapView.GetWaveColor(i+2));
                Assert.Equal(Color.Yellow, wavemapView.GetWaveColor(i+3));
                Assert.Equal(Color.Cyan, wavemapView.GetWaveColor(i+4));
            }
        }
    }
}
