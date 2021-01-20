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
grid_size (8192, 0, 0)
group_size (512, 0, 0)
wave_size 64
comment -");
            Assert.True(paramsResult.TryGetResult(out var ps, out _));
            Assert.False(ps.NDRange3D);
            Assert.Equal<uint>(512, ps.GroupSizeX);
            Assert.Equal<uint>(1, ps.GroupSizeY);
            Assert.Equal<uint>(1, ps.GroupSizeZ);
            Assert.Equal<uint>(8192, ps.GridSizeX);
            Assert.Equal<uint>(1, ps.GridSizeY);
            Assert.Equal<uint>(1, ps.GridSizeZ);
            Assert.Equal<uint>(16, ps.DimX);
            Assert.Equal<uint>(0, ps.DimY);
            Assert.Equal<uint>(0, ps.DimZ);

            paramsResult = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 2048, 256)
group_size (512, 256, 128)
wave_size 64
comment -");
            Assert.True(paramsResult.TryGetResult(out ps, out _));
            Assert.True(ps.NDRange3D);
            Assert.Equal<uint>(512, ps.GroupSizeX);
            Assert.Equal<uint>(256, ps.GroupSizeY);
            Assert.Equal<uint>(128, ps.GroupSizeZ);
            Assert.Equal<uint>(8192, ps.GridSizeX);
            Assert.Equal<uint>(2048, ps.GridSizeY);
            Assert.Equal<uint>(256, ps.GridSizeZ);
            Assert.Equal<uint>(16, ps.DimX);
            Assert.Equal<uint>(8, ps.DimY);
            Assert.Equal<uint>(2, ps.DimZ);

            paramsResult = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 2, 1)
group_size (512, 0, 0)
wave_size 64
comment -");
            Assert.False(paramsResult.TryGetResult(out _, out var error));
            Assert.Equal("Could not read the dispatch parameters file. If GridY is greater than one, GroupY cannot be zero.", error.Message);

            paramsResult = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 1, 2)
group_size (512, 0, 0)
wave_size 64
comment -");
            Assert.False(paramsResult.TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. If GridZ is greater than one, GroupZ cannot be zero.", error.Message);
        }

        [Fact]
        public void InvalidGridAndGroupSizeTest()
        {
            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (0, 0, 0)
group_size (0, 0, 0)
wave_size 64
comment -").TryGetResult(out _, out var error));
            Assert.Equal("Could not read the dispatch parameters file. GridX cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (64, 0, 0)
group_size (0, 0, 0)
wave_size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. GroupX cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (128, 0, 0)
group_size (512, 0, 0)
wave_size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. GroupX cannot be bigger than GridX.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (512, 128, 1)
group_size (512, 512, 1)
wave_size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. GroupY cannot be bigger than GridY.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (512, 512, 1)
group_size (512, 512, 128)
wave_size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. GroupZ cannot be bigger than GridZ.", error.Message);
        }

        [Fact]
        public void InvalidWaveSizeTest()
        {
            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (128, 0, 0)
group_size (32, 0, 0)
wave_size 0
comment -").TryGetResult(out _, out var error));
            Assert.Equal("Could not read the dispatch parameters file. WaveSize cannot be zero.", error.Message);

            Assert.False(BreakStateDispatchParameters.Parse(@"
grid_size (128, 0, 0)
group_size (32, 0, 0)
wave_size 64
comment -").TryGetResult(out _, out error));
            Assert.Equal("Could not read the dispatch parameters file. WaveSize cannot be bigger than GroupX.", error.Message);
        }

        [Fact]
        public void StatusStringTest()
        {
            var result = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 0, 0)
group_size (512, 0, 0)
wave_size 64");
            Assert.True(result.TryGetResult(out var ps, out _));
            Assert.Equal("", ps.StatusString);

            result = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 0, 0)
group_size (512, 0, 0)
wave_size 64
comment ");
            Assert.True(result.TryGetResult(out ps, out _));
            Assert.Equal("", ps.StatusString);

            result = BreakStateDispatchParameters.Parse(@"
grid_size (8192, 0, 0)
group_size (512, 0, 0)
wave_size 64
comment status string");
            Assert.True(result.TryGetResult(out ps, out _));
            Assert.Equal("status string", ps.StatusString);
        }

        [Fact]
        public void InvalidFormatTest()
        {
            var result = BreakStateDispatchParameters.Parse(@"
global_size (8192, 0, 0)
local_size (512, 0, 0)
warp_size 32");
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal(
@"Could not read the dispatch parameters file. The following is an example of the expected file contents:

grid_size (2048, 1, 1)
group_size (512, 1, 1)
wave_size 64
comment optional comment", error.Message);
        }
    }
}
