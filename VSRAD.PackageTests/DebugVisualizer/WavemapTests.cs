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

            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12);

            uint expected = 313;
            for (int i = 0; i < 10; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    Assert.Equal(expected, wavemapView[j, i].BreakLine);
                    expected += 313;
                }
            }
        }

        [Fact]
        public void ColorAssignTest()
        {
            // assume that all waves hitted the same breakpoint
            for (uint i = 3, j = 313; i < 360; i += 18)
                _data[i] = j;

            /* Red, Blue, Green, Yellow, Cyan */
            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12);

            for (int i = 0; i < 10; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    Assert.Equal(Color.Red, wavemapView[j, i].BreakColor);
                }
            }

            // now lets assume that all waves hitted unique breakpoint
            for (uint i = 3, j = 313; i < 360; i += 18, j += 313)
                _data[i] = j;

            wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12);

            Assert.Equal(Color.Red, wavemapView[0, 0].BreakColor);
            Assert.Equal(Color.Blue, wavemapView[1, 0].BreakColor);
            Assert.Equal(Color.Green, wavemapView[0, 1].BreakColor);
            Assert.Equal(Color.Yellow, wavemapView[1, 1].BreakColor);
            Assert.Equal(Color.Cyan, wavemapView[0, 2].BreakColor);

            Assert.Equal(Color.Red, wavemapView[1, 2].BreakColor);
            Assert.Equal(Color.Blue, wavemapView[0, 3].BreakColor);
            Assert.Equal(Color.Green, wavemapView[1, 3].BreakColor);
            Assert.Equal(Color.Yellow, wavemapView[0, 4].BreakColor);
            Assert.Equal(Color.Cyan, wavemapView[1, 4].BreakColor);

            Assert.Equal(Color.Red, wavemapView[0, 5].BreakColor);
            Assert.Equal(Color.Blue, wavemapView[1, 5].BreakColor);
            Assert.Equal(Color.Green, wavemapView[0, 6].BreakColor);
            Assert.Equal(Color.Yellow, wavemapView[1, 6].BreakColor);
            Assert.Equal(Color.Cyan, wavemapView[0, 7].BreakColor);

            Assert.Equal(Color.Red, wavemapView[1, 7].BreakColor);
            Assert.Equal(Color.Blue, wavemapView[0, 8].BreakColor);
            Assert.Equal(Color.Green, wavemapView[1, 8].BreakColor);
            Assert.Equal(Color.Yellow, wavemapView[0, 9].BreakColor);
            Assert.Equal(Color.Cyan, wavemapView[1, 9].BreakColor);
        }
    }
}
