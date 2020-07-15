using System.Xml.Serialization;
using VSRAD.Package.DebugVisualizer.Wavemap;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class WavemapTests
    {
        // 4 groups, 1 watch + system, group size 11
        private readonly uint[] _data = new uint[]
        {
            777, 0, 15, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 100, 0, 100, 0, 0, 0,  // 1-st group, break on line 15, not-empty exec-mask
            777, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0, 1, 1, 1,       // 2-nd group, empty exec-mask
            777, 0, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100, 0, 100, 0, 0, 0, // 3-rd group, break on line 105, not-empty exec-mask
            777, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 1, 1,       // 4-th group, empty exec-mask
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,       // extra data
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,       // extra data
        };

        [Fact]
        public void IsActiveGroupTest()
        {
            var wavemapView = new WavemapView(_data, groupSize: 11, laneDataSize: 2, groupCount: 4);
            for (int i = 0; i < 10; ++i)
                Assert.Equal(i < 4, wavemapView.IsActiveGroup(i));
        }

        [Fact]
        public void GroupExecutedTest()
        {
            var wavemapView = new WavemapView(_data, groupSize: 11, laneDataSize: 2, groupCount: 4);

            var executionMap = new bool[] { true, false, true, false };
            for (int i = 0; i < 4; ++i)
                Assert.Equal(executionMap[i], wavemapView.GroupExecuted(i));
        }

        [Fact]
        public void BreapointLineTest()
        {
            var wavemapView = new WavemapView(_data, groupSize: 11, laneDataSize: 2, groupCount: 4);

            var breakpointMap = new int[] { 15, 1, 105, 0 };
            for (int i = 0; i < 4; ++i)
                Assert.Equal(breakpointMap[i], wavemapView.GetBreakpointLine(i));
        }
    }
}
