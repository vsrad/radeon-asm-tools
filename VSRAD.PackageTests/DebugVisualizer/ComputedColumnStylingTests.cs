using System.Collections;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
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

        [Theory]
        [InlineData(1)] // wave size is too small
        [InlineData(9)] // wave size is too small
        [InlineData(65)] // wave size is too large
        [InlineData(128)] // wave size is too large
        public void LaneMaskingInvalidWaveSizeTest(uint waveSize)
        {
            var system = new uint[waveSize];
            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false, WaveSize = waveSize }, new ColumnStylingOptions(), groupSize: waveSize, system: new WatchView(system));

            for (int i = 0; i < waveSize; ++i)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) == 0); // lane masking does not apply, all columns are active
        }

        [Fact]
        public void LaneMaskingIncompleteGroupTest()
        {
            var system = new uint[12];
            system[8] = 0b11_0111_0111;

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false, WaveSize = 10 }, new ColumnStylingOptions(), groupSize: 12, system: new WatchView(system));

            Assert.True((styling.ColumnState[0] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[1] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[2] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.False((styling.ColumnState[3] & ColumnStates.Inactive) == 0); // 0 = inactive
            Assert.True((styling.ColumnState[4] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[5] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[6] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.False((styling.ColumnState[7] & ColumnStates.Inactive) == 0); // 0 = inactive
            Assert.True((styling.ColumnState[8] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[9] & ColumnStates.Inactive) == 0); // 1 = active
            Assert.True((styling.ColumnState[10] & ColumnStates.Inactive) == 0); // active (wave offset + 8 is out of bounds, masking does not apply)
            Assert.True((styling.ColumnState[11] & ColumnStates.Inactive) == 0); // active (wave offset + 8 is out of bounds, masking does not apply)
        }

        [Fact]
        public void LaneGroupingTinyGroupTest()
        {
            // No assertions, this test simply hangs if we don't handle groupSize < laneGrouping in the code
            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { LaneGrouping = 4 }, new ColumnStylingOptions(), groupSize: 3, system: null);
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
        public void MagicNumberWithIncompleteGroupTest()
        {
            var system = new uint[144];
            system[0] = 0x7;
            system[32] = 0x5;
            system[64] = 0x7;
            system[96] = 0x5;
            system[128] = 0x5;

            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7, WaveSize = 32 };
            var styling = new ComputedColumnStyling();
            styling.Recompute(visualizerOptions, new ColumnStylingOptions(), groupSize: 144, system: new WatchView(system));

            for (int i = 0; i < 32; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 32; i < 64; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 64; i < 96; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 96; i < 128; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 128; i < 144; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
        }
    }
}
