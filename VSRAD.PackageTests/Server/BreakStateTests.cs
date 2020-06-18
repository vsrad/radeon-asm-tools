using System;
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
        public async Task WatchViewTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var breakStateData = new BreakStateData(
                watches: new ReadOnlyCollection<string>(new[] { "local_id", "group_id", "group_size" }),
                file: new Package.Options.OutputFile("/home/journey", "hermes", true),
                fileTimestamp: default,
                outputByteCount: 4096,
                outputOffset: 0);

            var data = new int[1024]; // 2 groups by 2 waves (1 wave = 64 lanes), each containing 1 system dword and 3 watch dwords 
            for (int group = 0; group < 2; ++group)
            {
                for (int wave = 0; wave < 2; ++wave)
                {
                    for (int lane = 0; lane < 64; ++lane)
                    {
                        int flatLaneIdx = group * 128 + wave * 64 + lane;
                        int laneDataOffset = 4 * flatLaneIdx;
                        data[laneDataOffset + 0] = flatLaneIdx; // system = global id
                        data[laneDataOffset + 1] = lane;        // first watch = local id
                        data[laneDataOffset + 2] = group;       // second watch = group id
                        data[laneDataOffset + 3] = 128;         // third watch = group size (constant 128)
                    }
                }
            }
            var group1Bin = new byte[sizeof(int) * 512];
            Buffer.BlockCopy(data, 0, group1Bin, 0, group1Bin.Length);
            var group2Bin = new byte[sizeof(int) * 512];
            Buffer.BlockCopy(data, group1Bin.Length, group2Bin, 0, group2Bin.Length);

            // Group 1
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group1Bin }, (_) => { });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, 0, 128, 2);
            Assert.Null(warning);

            var system = breakStateData.GetSystem();
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i, (int)system[i]);
            var watchLocalId = breakStateData.GetWatch("local_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            var watchGroupId = breakStateData.GetWatch("group_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(0, (int)watchGroupId[i]);
            var watchGroupSize = breakStateData.GetWatch("group_size");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128, (int)watchGroupSize[i]);

            // Group 2
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group2Bin }, (_) => { });
            warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, 1, 128, 2);
            Assert.Null(warning);

            system = breakStateData.GetSystem();
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128 + i, (int)system[i]);
            watchLocalId = breakStateData.GetWatch("local_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            watchGroupId = breakStateData.GetWatch("group_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(1, (int)watchGroupId[i]);
            watchGroupSize = breakStateData.GetWatch("group_size");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128, (int)watchGroupSize[i]);

            // Switching to a smaller group that was already fetched doesn't send any requests
            // Group 1 of size 64 = second half of group 1 of size 128
            warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, 1, 64, 4);
            Assert.Null(warning);

            system = breakStateData.GetSystem();
            for (var i = 0; i < 64; ++i)
                Assert.Equal(64 + i, (int)system[i]);
            watchLocalId = breakStateData.GetWatch("local_id");
            for (var i = 0; i < 64; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            watchGroupId = breakStateData.GetWatch("group_id");
            for (var i = 0; i < 64; ++i)
                Assert.Equal(0, (int)watchGroupId[i]);
            watchGroupSize = breakStateData.GetWatch("group_size");
            for (var i = 0; i < 64; ++i)
                Assert.Equal(128, (int)watchGroupSize[i]);
        }

        [Fact]
        public async Task EmptyResultRangeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var breakStateData = new BreakStateData(
                watches: new ReadOnlyCollection<string>(new[] { "h" }),
                file: new Package.Options.OutputFile("/home/kyubey/projects", "log.tar", true),
                fileTimestamp: default,
                outputByteCount: 4096,
                outputOffset: 0);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>() },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects", "log.tar" }, command.FilePath);
            });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, 0, 512, 1);
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", warning);
            // Data is set to 0 if unavailable
            Assert.Equal(0u, breakStateData.GetSystem()[0]);
        }

        [Fact]
        public async Task NGroupViolationProducesAWarningButFetchesResultsTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var breakStateData = new BreakStateData(
                watches: new ReadOnlyCollection<string>(new[] { "h" }),
                file: new Package.Options.OutputFile("/home/kyubey/projects", "log.tar", true),
                fileTimestamp: default,
                outputByteCount: 4096,
                outputOffset: 0);

            Assert.Equal(2, breakStateData.GetGroupCount(groupSize: 256, nGroups: 4));

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = new byte[2048] }, (_) => { });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, 0, 256, nGroups: 4);

            Assert.Equal("Output file has fewer groups than requested (NGroups = 4, but the file contains only 2)", warning);
        }
    }
}
