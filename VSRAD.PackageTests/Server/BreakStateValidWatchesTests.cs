using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateValidWatchesTests
    {
        [Theory]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint: 0\r\ninstance 0 watchnames: a;tide\r\ninstance 0 watchitems: [1,[1,1],1]")]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint: 0\r\ninstance 0 watchnames: a;tide\r\ninstance 0 watchitems: 1,[1,1],1]")]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint: 0\r\ninstance 0 watchnames: a;tide\r\ninstance 0 watchitems: 0,]]]")]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint: 0\r\ninstance 0 watchnames: a;tide\r\ninstance 0 watchitems: [1,1")]
        public void InvalidInstanceMetadataTest(string validWatches)
        {
            string dispatchParams = @"
grid_size (512, 1, 1)
group_size (512, 1, 1)
wave_size 64";

            var debugData = new byte[512 * 5 * sizeof(uint)];
            Assert.False(BreakState.CreateBreakState(new BreakTarget(new[] { new BreakpointInfo("", 0, 1, false) }, BreakTargetSelector.SingleNext, "", 0, ""),
                validWatches, dispatchParams, new BreakStateOutputFile(new[] { "" }, true, 0, default, debugData.Length / 4), debugData).TryGetResult(out _, out var error));
            Assert.Equal($@"Could not read the valid watches file.

The following is an example of the expected file contents:

max items per instance including system watch: 10
instance 0 breakpoint: 0
Instance 0 watchnames: a;b;c
Instance 0 watchitems: [1,[1,0,1,1],[1,[1,1],0,[1],[],1]]

While the actual contents are:

{validWatches}", error.Message);
        }
    }
}
