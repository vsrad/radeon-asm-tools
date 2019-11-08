using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateTests
    {
        [Fact]
        public async Task EmptyResultRangeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var breakState = new BreakState(new Package.Options.OutputFile("/home/kyubey/projects", "log.tar", true),
                default, outputByteCount: 4096, outputOffset: 0, watches: new ReadOnlyCollection<string>(new[] { "h" }), channel.Object);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects", "log.tar" }, command.FilePath);
            });
            var result = await breakState.ChangeGroupAsync(0, 512);
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", error.Message);
        }
    }
}
