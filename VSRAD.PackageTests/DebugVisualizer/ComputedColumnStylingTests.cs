using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class ComputedColumnStylingTests
    {
        private WatchView GetSystemView(uint[] system, int groupSize, int waveSize, int groupIndex = 0)
        {
            byte[] systemBytes = new byte[system.Length * 4];
            Buffer.BlockCopy(system, 0, systemBytes, 0, systemBytes.Length);
            var data = new BreakStateData(new ReadOnlyCollection<string>(new List<string>()),
                new BreakStateOutputFile("", false, 0, default, dwordCount: system.Length), systemBytes);
            _ = data.ChangeGroupWithWarningsAsync(null, groupIndex: groupIndex, groupSize: groupSize, waveSize: waveSize, nGroups: 0).Result;
            var view = data.GetSystem();
            Assert.NotNull(view);
            return view;
        }

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
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: 64, waveSize: 64, system: GetSystemView(system, groupSize: 64, waveSize: 64));

            for (int i = 0; i < 5; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 5; i < 23; i++)
                Assert.False((styling.ColumnState[i] & ColumnStates.Inactive) != 0);
            for (int i = 24; i < 45; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);

            Assert.False((styling.ColumnState[45] & ColumnStates.Inactive) != 0);

            for (int i = 46; i < 64; i++)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0);

            styling.Recompute(new VisualizerOptions { MaskLanes = false, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: 64, waveSize: 64, system: GetSystemView(system, groupSize: 64, waveSize: 64));

            for (int i = 0; i < 64; i++)
                Assert.False((styling.ColumnState[45] & ColumnStates.Inactive) != 0);
        }

        [Theory]
        [InlineData(1)] // wave size is too small
        [InlineData(9)] // wave size is too small
        [InlineData(65)] // wave size is too large
        [InlineData(128)] // wave size is too large
        public void LaneMaskingInvalidWaveSizeTest(int waveSize)
        {
            var system = new uint[waveSize];
            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: (uint)waveSize, waveSize: (uint)waveSize, system: GetSystemView(system, groupSize: waveSize, waveSize: waveSize));

            for (int i = 0; i < waveSize; ++i)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) == 0); // lane masking does not apply, all columns are active
        }

        [Fact]
        public void LaneMaskingIncompleteGroupTest()
        {
            var system = new uint[12];
            system[8] = 0b11_0111_0111;

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: 12, waveSize: 10, system: GetSystemView(system, groupSize: 12, waveSize: 10));

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

        [Theory]
        [InlineData(12, 24, 12, 0)] // group size exceeds data size
        [InlineData(36, 24, 12, 1)] // some of the items in the last group are out of bounds
        [InlineData(60, 24, 10, 1)] // display group size is 24, data group size is 30 (check that we don't exceed the bounds of ColumnState[])
        public void LaneMaskingOutputSizeNotDivisibleByGroupSizeTest(int dataSize, int groupSize, int waveSize, int groupIndex)
        {
            var system = new uint[dataSize];

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = true, MagicNumber = 0 }, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: (uint)groupSize, waveSize: (uint)waveSize, system: GetSystemView(system, groupSize: groupSize, waveSize: waveSize, groupIndex: groupIndex));

            for (int i = 0; i < 12; ++i)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0); // all lanes are inactive (exec mask = 0)
        }

        [Fact]
        public void LaneGroupingTinyGroupTest()
        {
            // No assertions, this test simply hangs if we don't handle groupSize < laneGrouping in the code
            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 4 }, new ColumnStylingOptions(), groupSize: 3, waveSize: 3, system: null);
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
            styling.Recompute(visualizerOptions, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: 256, waveSize: 64, system: GetSystemView(system, groupSize: 256, waveSize: 64));

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

            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7 };
            var styling = new ComputedColumnStyling();
            styling.Recompute(visualizerOptions, new VisualizerAppearance(), new ColumnStylingOptions(),
                groupSize: 144, waveSize: 32, system: GetSystemView(system, groupSize: 144, waveSize: 32));

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
