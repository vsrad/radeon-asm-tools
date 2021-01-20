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

            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12, groupCount: 10);

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

            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12, groupCount: 10);

            for (int i = 0; i < 10; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    Assert.Equal(WavemapView.Blue, wavemapView[j, i].BreakColor);
                }
            }

            // now lets assume that all waves hitted unique breakpoint
            for (uint i = 3, j = 313; i < 360; i += 18, j += 313)
                _data[i] = j;

            wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12, groupCount: 10);

            Assert.Equal(WavemapView.Blue, wavemapView[0, 0].BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView[1, 0].BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView[0, 1].BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView[1, 1].BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView[0, 2].BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView[1, 2].BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView[0, 3].BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView[1, 3].BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView[0, 4].BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView[1, 4].BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView[0, 5].BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView[1, 5].BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView[0, 6].BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView[1, 6].BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView[0, 7].BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView[1, 7].BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView[0, 8].BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView[1, 8].BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView[0, 9].BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView[1, 9].BreakColor);
        }

        [Fact]
        public void IsValidWaveTest()
        {
            var wavemapView = new WavemapView(_data, waveSize: 6, laneDataSize: 3, groupSize: 12, groupCount: 10);

            for (int i = 0; i < 20; ++i)
                for (int j = 0; j < 5; ++j)
                    Assert.Equal(i < 10 && j < 2, wavemapView[j, i].IsVisible);
        }

        [Fact]
        public void WaveSizeSmallerThanBreakLineOffsetTest()
        {
            var view = new WavemapView(new uint[68], waveSize: 1, laneDataSize: 1, groupSize: 17, groupCount: 4);

            // When accessing the last wave of the last group, the break line (lane #1) is out of range with wave size = 1
            var lastWave = view[16, 3];
            // The wave is invisible because we cannot get the break line 
            Assert.False(lastWave.IsVisible);
        }

        [Theory]
        [InlineData(8), InlineData(9)]
        public void WaveSizeSmallerThanExecMaskOffsetTest(int waveSize)
        {
            var wavesPerGroup = 2;
            var groupCount = 2;

            var data = new uint[waveSize * wavesPerGroup * groupCount];
            for (int i = 0; i < waveSize * wavesPerGroup * groupCount; ++i)
                data[i] = ~0u;

            var view = new WavemapView(data, waveSize: waveSize, laneDataSize: 1, groupSize: waveSize * wavesPerGroup, groupCount: groupCount)
            {
                CheckInactiveLanes = true
            };

            // When accessing the last wave of the last group, the exec mask (lanes #8-9) is out of range with wave size < 9
            var lastWave = view[wavesPerGroup - 1, groupCount - 1];
            Assert.True(lastWave.IsVisible);
            Assert.False(lastWave.PartialMask); // exec mask is not checked
        }
    }
}
