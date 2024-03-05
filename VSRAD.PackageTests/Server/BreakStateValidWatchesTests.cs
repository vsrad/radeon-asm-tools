using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateValidWatchesTests
    {
        [Theory]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint id: 0\r\ninstance 0 valid watches: 1,[1,1],1]")]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint id: 0\r\ninstance 0 valid watches: 0,]]]")]
        [InlineData("max items per instance including system watch: 5\r\ninstance 0 breakpoint id: 0\r\ninstance 0 valid watches: [1,1")]
        public void InvalidInstanceMetadataTest(string validWatches)
        {
            string dispatchParams = @"
grid_size (512, 1, 1)
group_size (512, 1, 1)
wave_size 64";

            var debugData = new byte[512 * 5 * sizeof(uint)];
            Assert.False(BreakState.CreateBreakState(new BreakTarget(new[] { new BreakpointInfo("", 0, 1, false) }, BreakTargetSelector.SingleNext, "", 0, ""), new[] { "m", "c", "ride" },
                validWatches, dispatchParams, new BreakStateOutputFile("", true, 0, default, debugData.Length / 4), debugData, null).TryGetResult(out _, out var error));
            Assert.Equal($@"Could not read the valid watches file.

The following is an example of the expected file contents:

max items per instance including system watch: 10
instance 0 breakpoint id: 0
Instance 0 valid watches: [1,[1,0,1,1],[1,[1,1],0,[1],[],1]]

Where ""breakpoint id"" refers to an item from the target breakpoints file and ""valid watches"" is a list referring to items from the target watches file.

The actual file contents are:

{validWatches}", error.Message);
        }
    }
}
