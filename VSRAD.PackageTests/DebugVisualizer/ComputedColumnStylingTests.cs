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

        [Fact]
        public void LaneMaskingTinyGroupTest()
        {
            var system = new uint[32];
            system[8] = 0b0111_0111;

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new ColumnStylingOptions(), groupSize: 7, system: new WatchView(system));

            Assert.False((styling.ColumnState[0] & ColumnStates.Inactive) != 0); // 1 = active
            Assert.False((styling.ColumnState[1] & ColumnStates.Inactive) != 0); // 1 = active
            Assert.False((styling.ColumnState[2] & ColumnStates.Inactive) != 0); // 1 = active
            Assert.True((styling.ColumnState[3] & ColumnStates.Inactive) != 0); // 0 = inactive
            Assert.False((styling.ColumnState[4] & ColumnStates.Inactive) != 0); // 1 = active
            Assert.False((styling.ColumnState[5] & ColumnStates.Inactive) != 0); // 1 = active
            Assert.False((styling.ColumnState[6] & ColumnStates.Inactive) != 0); // 1 = active
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
        public void MagicNumberWithNonConstantWaveSizeTest()
        {
            var system = new uint[128];
            system[0] = 0x7;
            system[32] = 0x5;
            system[64] = 0x7;
            system[96] = 0x5;

            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7, WaveSize = 32 };
            var styling = new ComputedColumnStyling();
            styling.Recompute(visualizerOptions, new ColumnStylingOptions(), groupSize: 128, system: new WatchView(system));

            for (int i = 0; i < 32; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 32; i < 64; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 64; i < 96; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 96; i < 128; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
        }
    }
}
