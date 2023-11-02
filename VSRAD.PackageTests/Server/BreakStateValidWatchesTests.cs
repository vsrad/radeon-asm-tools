using System;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateValidWatchesTests
    {
        [Theory]
        [InlineData("Max items per instance including System watch: 5\r\nInstance 0 names: a;tide\r\nInstance 0 items: [1,[1,1],1]")]
        [InlineData("Max items per instance including System watch: 5\r\nInstance 0 names: a;tide\r\nInstance 0 items: 1,[1,1],1]")]
        [InlineData("Max items per instance including System watch: 5\r\nInstance 0 names: a;tide\r\nInstance 0 items: 0,]]]")]
        [InlineData("Max items per instance including System watch: 5\r\nInstance 0 names: a;tide\r\nInstance 0 items: [1,1")]
        public void InvalidInstanceMetadataTest(string validWatches)
        {
            string dispatchParams = @"
grid_size (512, 1, 1)
group_size (512, 1, 1)
wave_size 64";

            var debugData = new byte[512 * 5 * sizeof(uint)];
            Assert.False(BreakState.CreateBreakState(validWatches, dispatchParams,
                new BreakStateOutputFile(new[] { "" }, true, 0, default, debugData.Length / 4), debugData, Array.Empty<BreakpointInfo>()).TryGetResult(out _, out var error));
            Assert.Equal($@"Could not read the valid watches file.

The following is an example of the expected file contents:

Max items per instance including System watch: 10
Instance 0 names: a;b;c
Instance 0 items: [1,[1,0,1,1],[1,[1,1],0,[1],[],1]]

While the actual contents are:

{validWatches}", error.Message);
        }
    }
}
