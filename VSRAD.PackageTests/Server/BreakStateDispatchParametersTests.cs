using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public sealed class BreakStateDispatchParametersTests
    {
        [Fact]
        public void NDRangeTest()
        {
            var paramsResult = BreakStateDispatchParameters.Parse(@"
grid size (8192, 0, 0)
group size (512, 0, 0)
wave size 64
comment -");
            Assert.True(paramsResult.TryGetResult(out var ps, out _));
            Assert.False(ps.NDRange3D);
            Assert.Equal<uint>(512, ps.GroupSize);
            Assert.Equal<uint>(16, ps.DimX);
            Assert.Equal<uint>(0, ps.DimY);
            Assert.Equal<uint>(0, ps.DimZ);

            paramsResult = BreakStateDispatchParameters.Parse(@"
grid size (8192, 2048, 256)
group size (512, 256, 128)
wave size 64
comment -");
            Assert.True(paramsResult.TryGetResult(out ps, out _));
            Assert.True(ps.NDRange3D);
            Assert.Equal<uint>(512, ps.GroupSize);
            Assert.Equal<uint>(16, ps.DimX);
            Assert.Equal<uint>(8, ps.DimY);
            Assert.Equal<uint>(2, ps.DimZ);

            paramsResult = BreakStateDispatchParameters.Parse(@"
grid size (8192, 1, 1)
group size (512, 0, 0)
wave size 64
comment -");
            Assert.False(paramsResult.TryGetResult(out _, out var error));
            Assert.Equal("Could not set dispatch parameters from the status file. If GridY and GridZ are set, GroupY and GroupZ cannot be zero.", error.Message);
        }

        [Fact]
        public void InvalidGridAndGroupSizeTest()
        {
            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (0, 0, 0)
group size (0, 0, 0)
wave size 64
comment -").TryGetResult(out _, out var error));
            Assert.Equal("Could not set dispatch parameters from the status file. GridX cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (64, 0, 0)
group size (0, 0, 0)
wave size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not set dispatch parameters from the status file. GroupX cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (128, 0, 0)
group size (512, 0, 0)
wave size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not set dispatch parameters from the status file. GroupX cannot be bigger than GridX.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (512, 128, 1)
group size (512, 512, 1)
wave size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not set dispatch parameters from the status file. GroupY cannot be bigger than GridY.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (512, 512, 1)
group size (512, 512, 128)
wave size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not set dispatch parameters from the status file. GroupZ cannot be bigger than GridZ.", error.Message);
        }

        [Fact]
        public void InvalidWaveSizeTest()
        {
            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (128, 0, 0)
group size (32, 0, 0)
wave size 0
comment -").TryGetResult(out _, out var error));
            Assert.Equal("Could not set dispatch parameters from the status file. WaveSize cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid size (128, 0, 0)
group size (32, 0, 0)
wave size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not set dispatch parameters from the status file. WaveSize cannot be bigger than GroupX.", error.Message);
        }
    }
}
