using System.Collections;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.Package.DebugVisualizer.Tests
{
    public class ComputedColumnStylingTests
    {
        [Fact]
        public void LaneMaskingTest()
        {
            var maskLowBits = new bool[32];
            for (int i = 5; i < 23; i++)
                maskLowBits[i] = true;
            var maskHighBits = new bool[32];
            maskHighBits[13] = true;

            var system = new uint[64];

            var tmp = new int[1];
            new BitArray(maskLowBits).CopyTo(tmp, 0);
            system[8] = (uint)tmp[0];
            new BitArray(maskHighBits).CopyTo(tmp, 0);
            system[9] = (uint)tmp[0];

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new ColumnStylingOptions(), groupSize: 64, system: new WatchView(system));

            for (int i = 0; i < 5; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 5; i < 23; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 24; i < 45; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);

            Assert.False((styling.ColumnState[45] & ColumnStates.Inactive) != 0);

            for (int i = 46; i < 64; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);

            styling.Recompute(new VisualizerOptions { MaskLanes = false, CheckMagicNumber = false }, new ColumnStylingOptions(), groupSize: 64, system: new WatchView(system));

            for (int i = 0; i < 64; i++)
                Assert.False((styling.ColumnState[45] & ColumnStates.Inactive) != 0);
        }

        [Fact]
        public void MagicNumberCheckTest()
        {
            var system = new uint[256];
            system[0] = 0x7;
            system[64] = 0x5;
            system[128] = 0x7;

            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7 };
            var styling = new ComputedColumnStyling();
            styling.Recompute(visualizerOptions, new ColumnStylingOptions(), groupSize: 256, system: new WatchView(system));

            for (int i = 0; i < 63; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 64; i < 128; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 128; i < 192; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 192; i < 256; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
        }

        [Fact]
        public void SliceWatchViewTest()
        {
            var data = new uint[] { 666, 0, 666, 10, 666, 20, 666, 30, 666, 40, 666, 1, 666,
                11, 666, 21, 666, 31, 666, 41, 666, 2, 666, 12, 666, 22, 666, 32, 666, 42, 666, 3, 666, 13,
                666, 23, 666, 33, 666, 43 };

            var sliceWatch = new SliceWatchWiew(data, 2, 5, 1, 1, 1);
            
            Assert.Equal((uint)0, sliceWatch[0, 0]);
            Assert.Equal((uint)10, sliceWatch[0, 1]);
            Assert.Equal((uint)20, sliceWatch[0, 2]);
            Assert.Equal((uint)30, sliceWatch[0, 3]);
            Assert.Equal((uint)40, sliceWatch[0, 4]);
            Assert.Equal((uint)1, sliceWatch[0, 5]);
            Assert.Equal((uint)11, sliceWatch[0, 6]);
            Assert.Equal((uint)21, sliceWatch[0, 7]);
            Assert.Equal((uint)31, sliceWatch[0, 8]);
            Assert.Equal((uint)41, sliceWatch[0, 9]);
            Assert.Equal((uint)2, sliceWatch[1, 0]);
            Assert.Equal((uint)12, sliceWatch[1, 1]);
            Assert.Equal((uint)22, sliceWatch[1, 2]);
            Assert.Equal((uint)32, sliceWatch[1, 3]);
            Assert.Equal((uint)42, sliceWatch[1, 4]);
            Assert.Equal((uint)3, sliceWatch[1, 5]);
            Assert.Equal((uint)13, sliceWatch[1, 6]);
            Assert.Equal((uint)23, sliceWatch[1, 7]);
            Assert.Equal((uint)33, sliceWatch[1, 8]);
            Assert.Equal((uint)43, sliceWatch[1, 9]);
        }
    }
}
