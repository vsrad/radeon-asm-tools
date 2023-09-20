using VSRAD.Package.DebugVisualizer.Wavemap;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class WavemapTests
    {
        [Fact]
        public void ColorAssignTest()
        {
            // Same breakpoint for all waves
            var wavemapView = new WavemapView((uint groupIndex, uint waveIndex, out uint[] systemData) =>
            {
                systemData = new uint[16];
                systemData[Package.Server.BreakStateData.SystemBreakLineLane] = 313;
                return true;
            });

            for (uint g = 0; g < 10; ++g)
            {
                for (uint w = 0; w < 2; ++w)
                {
                    Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(g, w, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
                }
            }

            // Unique breakpoint per wave
            wavemapView = new WavemapView((uint groupIndex, uint waveIndex, out uint[] systemData) =>
            {
                systemData = new uint[16];
                systemData[Package.Server.BreakStateData.SystemBreakLineLane] = 313 * groupIndex + waveIndex;
                return true;
            });

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(0, 0, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(1, 0, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(0, 1, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(1, 1, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(0, 2, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(1, 2, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(0, 3, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(1, 3, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(0, 4, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(1, 4, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(0, 5, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(1, 5, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(0, 6, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(1, 6, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(0, 7, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);

            Assert.Equal(WavemapView.Blue, wavemapView.GetWaveInfo(1, 7, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Red, wavemapView.GetWaveInfo(0, 8, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Green, wavemapView.GetWaveInfo(1, 8, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Violet, wavemapView.GetWaveInfo(0, 9, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
            Assert.Equal(WavemapView.Pink, wavemapView.GetWaveInfo(1, 9, checkMagicNumber: null, checkInactiveLanes: false).BreakColor);
        }
    }
}
