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
            var data = new uint[] { 600, 0, 601, 10, 602, 20, 603, 30, 604, 40, 605, 1, 606,
                11, 607, 21, 608, 31, 609, 41, 610, 2, 611, 12, 612, 22, 613, 32, 614, 42, 615, 3, 616, 13,
                617, 23, 618, 33, 619, 43 };

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

            sliceWatch = new SliceWatchWiew(data, 2, 5, 1, 0, 1);

            Assert.Equal((uint)600, sliceWatch[0, 0]);
            Assert.Equal((uint)601, sliceWatch[0, 1]);
            Assert.Equal((uint)602, sliceWatch[0, 2]);
            Assert.Equal((uint)603, sliceWatch[0, 3]);
            Assert.Equal((uint)604, sliceWatch[0, 4]);
            Assert.Equal((uint)605, sliceWatch[0, 5]);
            Assert.Equal((uint)606, sliceWatch[0, 6]);
            Assert.Equal((uint)607, sliceWatch[0, 7]);
            Assert.Equal((uint)608, sliceWatch[0, 8]);
            Assert.Equal((uint)609, sliceWatch[0, 9]);
            Assert.Equal((uint)610, sliceWatch[1, 0]);
            Assert.Equal((uint)611, sliceWatch[1, 1]);
            Assert.Equal((uint)612, sliceWatch[1, 2]);
            Assert.Equal((uint)613, sliceWatch[1, 3]);
            Assert.Equal((uint)614, sliceWatch[1, 4]);
            Assert.Equal((uint)615, sliceWatch[1, 5]);
            Assert.Equal((uint)616, sliceWatch[1, 6]);
            Assert.Equal((uint)617, sliceWatch[1, 7]);
            Assert.Equal((uint)618, sliceWatch[1, 8]);
            Assert.Equal((uint)619, sliceWatch[1, 9]);
        }
    }
}
