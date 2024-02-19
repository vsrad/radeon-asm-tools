using System;
using System.Collections.Generic;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class ComputedColumnStylingTests
    {
        private static BreakState MakeBreakState(uint[] system, int groupSize, int waveSize, int groupIndex = 0)
        {
            byte[] systemBytes = new byte[system.Length * 4];
            Buffer.BlockCopy(system, 0, systemBytes, 0, systemBytes.Length);
            var breakState = new BreakState(BreakTarget.Empty, new Dictionary<string, WatchMeta>(),
                new BreakStateDispatchParameters(waveSize: (uint)waveSize, gridX: (uint)groupSize, gridY: 1, gridZ: 1, groupX: (uint)groupSize, groupY: 1, groupZ: 1, ""),
                new Dictionary<uint, uint>(), dwordsPerLane: 1,
                new BreakStateOutputFile(Array.Empty<string>(), false, 0, default, dwordCount: system.Length), checkMagicNumber: null, systemBytes);
            _ = breakState.ChangeGroupWithWarningsAsync(null, groupIndex: (uint)groupIndex).Result;
            return breakState;
        }

        [Theory]
        [InlineData(256, 64)]
        [InlineData(256, 32)]
        [InlineData(96, 64)] // Incomplete group
        public void LaneMaskingTest(int groupSize, int waveSize)
        {
            var system = new uint[(groupSize + waveSize - 1) / waveSize * waveSize];
            // EXEC mask = 1s for multiple of 4 lane ids
            for (var tid = 0; tid < system.Length; ++tid)
            {
                var wave = tid / waveSize;
                var lane = tid % waveSize;
                if (lane < 32)
                    system[wave * waveSize + 8] |= ((tid % 4 == 0) ? 1u : 0u) << lane;
                else
                    system[wave * waveSize + 9] |= ((tid % 4 == 0) ? 1u : 0u) << (lane - 32);
            }

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                MakeBreakState(system, groupSize: groupSize, waveSize: waveSize));

            for (var tid = 0; tid < groupSize; ++tid)
            {
                if (tid % 4 == 0)
                    Assert.False((styling.ColumnState[tid] & ColumnStates.Inactive) != 0);
                else
                    Assert.True((styling.ColumnState[tid] & ColumnStates.Inactive) != 0);
            }

            styling.Recompute(new VisualizerOptions { MaskLanes = false, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(),
                MakeBreakState(system, groupSize: groupSize, waveSize: waveSize));

            for (var tid = 0; tid < groupSize; ++tid)
                Assert.False((styling.ColumnState[tid] & ColumnStates.Inactive) != 0);
        }

        [Theory]
        [InlineData(32, 64, 32, 0)] // group size exceeds data size
        public void LaneMaskingOutputSizeNotDivisibleByGroupSizeTest(int dataSize, int groupSize, int waveSize, int groupIndex)
        {
            var system = new uint[dataSize];

            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = true, MagicNumber = 0 }, new VisualizerAppearance(), new ColumnStylingOptions(),
                MakeBreakState(system, groupSize: groupSize, waveSize: waveSize, groupIndex: groupIndex));

            for (int i = 0; i < 12; ++i)
                Assert.True((styling.ColumnState[i] & ColumnStates.Inactive) != 0); // all lanes are inactive (exec mask = 0)
        }

        [Fact]
        public void LaneGroupingTinyGroupTest()
        {
            // No assertions, this test simply hangs if we don't handle groupSize < laneGrouping in the code
            var styling = new ComputedColumnStyling();
            styling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 4 }, new ColumnStylingOptions(),
                MakeBreakState(new[] { 0u, 0u, 0u }, groupSize: 3, waveSize: 3));
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
                MakeBreakState(system, groupSize: 256, waveSize: 64));

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
                MakeBreakState(system, groupSize: 144, waveSize: 32));

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
