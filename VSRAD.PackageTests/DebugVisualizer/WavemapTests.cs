using VSRAD.Package.DebugVisualizer.Wavemap;
using VSRAD.Package.ProjectSystem;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class WavemapTests
    {
        [Fact]
        public void ColorAssignTest()
        {
            // Same breakpoint for all waves
            var wavemapView = new WavemapView(
                tryGetGlobalWaveMeta: (uint groupIndex, uint waveIndex, out uint breakpointIdx, out BreakpointInfo breakpoint, out ulong execMask) =>
            {
                (breakpointIdx, breakpoint, execMask) = (312, new BreakpointInfo("", 0, 0, false), ~0ul);
                return true;
            });

            for (uint g = 0; g < 10; ++g)
            {
                for (uint w = 0; w < 2; ++w)
                {
                    Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(g, w, checkInactiveLanes: false).BreakColor);
                }
            }

            // Unique breakpoint per wave
            wavemapView = new WavemapView(
                tryGetGlobalWaveMeta: (uint groupIndex, uint waveIndex, out uint breakpointIdx, out BreakpointInfo breakpoint, out ulong execMask) =>
                {
                    (breakpointIdx, breakpoint, execMask) = (303 * groupIndex + waveIndex, new BreakpointInfo("", 0, 0, false), ~0ul);
                    return true;
                });

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(0, 0, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(1, 0, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(0, 1, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(1, 1, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(0, 2, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(1, 2, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(0, 3, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(1, 3, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(0, 4, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(1, 4, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(0, 5, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(1, 5, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(0, 6, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(1, 6, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(0, 7, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(1, 7, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(0, 8, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(1, 8, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(0, 9, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(1, 9, checkInactiveLanes: false).BreakColor);
        }
    }
}
