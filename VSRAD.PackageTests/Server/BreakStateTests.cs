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

            var breakState = new BreakState(breakStateData, 666, 333, "", 0);

            // Group 1
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group1Bin }, (_) => { });
            var warning = await breakState.Data.ChangeGroupWithWarningsAsync(channel.Object, 0, 128);
            Assert.Null(warning);

            var system = breakState.Data.GetSystem();
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i, (int)system[i]);
            var watchLocalId = breakState.Data.GetWatch("local_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            var watchGroupId = breakState.Data.GetWatch("group_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(0, (int)watchGroupId[i]);
            var watchGroupSize = breakState.Data.GetWatch("group_size");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128, (int)watchGroupSize[i]);

            // Group 2
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group2Bin }, (_) => { });
            warning = await breakState.Data.ChangeGroupWithWarningsAsync(channel.Object, 1, 128);
            Assert.Null(warning);

            system = breakState.Data.GetSystem();
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128 + i, (int)system[i]);
            watchLocalId = breakState.Data.GetWatch("local_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            watchGroupId = breakState.Data.GetWatch("group_id");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(1, (int)watchGroupId[i]);
            watchGroupSize = breakState.Data.GetWatch("group_size");
            for (var i = 0; i < 128; ++i)
                Assert.Equal(128, (int)watchGroupSize[i]);

            // Switching to a smaller group that was already fetched doesn't send any requests
            // Group 1 of size 64 = second half of group 1 of size 128
            warning = await breakState.Data.ChangeGroupWithWarningsAsync(channel.Object, 1, 64);
            Assert.Null(warning);

            system = breakState.Data.GetSystem();
            for (var i = 0; i < 64; ++i)
                Assert.Equal(64 + i, (int)system[i]);
            watchLocalId = breakState.Data.GetWatch("local_id");
            for (var i = 0; i < 64; ++i)
                Assert.Equal(i % 64, (int)watchLocalId[i]);
            watchGroupId = breakState.Data.GetWatch("group_id");
            for (var i = 0; i < 64; ++i)
                Assert.Equal(0, (int)watchGroupId[i]);
            watchGroupSize = breakState.Data.GetWatch("group_size");
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

            var breakState = new BreakState(breakStateData, 666, 333, "", 0);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>() },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects", "log.tar" }, command.FilePath);
            });
            var warning = await breakState.Data.ChangeGroupWithWarningsAsync(channel.Object, 0, 512);
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", warning);
            // Data is set to 0 if unavailable
            Assert.Equal(0u, breakState.Data.GetSystem()[0]);
        }


        [Fact]
        public void SliceWatchViewTest()
        {
            var data = new uint[] { 600, 0, 601, 10, 602, 20, 603, 30, 604, 40, 605, 1, 606,
                11, 607, 21, 608, 31, 609, 41, 610, 2, 611, 12, 612, 22, 613, 32, 614, 42, 615, 3, 616, 13,
                617, 23, 618, 33, 619, 43 };

            var sliceWatch = new SliceWatchWiew(data, groupsInRow: 2, groupSize: 5, laneDataOffset: 1, laneDataSize: 2);

            Assert.Equal((uint)0, sliceWatch[0, 0]);
            Assert.Equal((uint)10, sliceWatch[0, 1]);
            Assert.Equal((uint)20, sliceWatch[0, 2]);
            Assert.Equal((uint)30, sliceWatch[0, 3]);
            Assert.Equal((uint)40, sliceWatch[0, 4]);
            Assert.Equal((uint)1, sliceWatch[0, 5]);
            Assert.Equal((uint)11, sliceWatch[0, 6]);
            Assert.Equal((uint)21, sliceWatch[0, 7]);
            Assert.Equal((uint)31, sliceWatch[0, 8]);
            Assert.Equal((uint)41, sliceWatch[0, 9]);
            Assert.Equal((uint)2, sliceWatch[1, 0]);
            Assert.Equal((uint)12, sliceWatch[1, 1]);
            Assert.Equal((uint)22, sliceWatch[1, 2]);
            Assert.Equal((uint)32, sliceWatch[1, 3]);
            Assert.Equal((uint)42, sliceWatch[1, 4]);
            Assert.Equal((uint)3, sliceWatch[1, 5]);
            Assert.Equal((uint)13, sliceWatch[1, 6]);
            Assert.Equal((uint)23, sliceWatch[1, 7]);
            Assert.Equal((uint)33, sliceWatch[1, 8]);
            Assert.Equal((uint)43, sliceWatch[1, 9]);

            sliceWatch = new SliceWatchWiew(data, groupsInRow: 2, groupSize: 5, laneDataOffset: 0, laneDataSize: 2);

            Assert.Equal((uint)600, sliceWatch[0, 0]);
            Assert.Equal((uint)601, sliceWatch[0, 1]);
            Assert.Equal((uint)602, sliceWatch[0, 2]);
            Assert.Equal((uint)603, sliceWatch[0, 3]);
            Assert.Equal((uint)604, sliceWatch[0, 4]);
            Assert.Equal((uint)605, sliceWatch[0, 5]);
            Assert.Equal((uint)606, sliceWatch[0, 6]);
            Assert.Equal((uint)607, sliceWatch[0, 7]);
            Assert.Equal((uint)608, sliceWatch[0, 8]);
            Assert.Equal((uint)609, sliceWatch[0, 9]);
            Assert.Equal((uint)610, sliceWatch[1, 0]);
            Assert.Equal((uint)611, sliceWatch[1, 1]);
            Assert.Equal((uint)612, sliceWatch[1, 2]);
            Assert.Equal((uint)613, sliceWatch[1, 3]);
            Assert.Equal((uint)614, sliceWatch[1, 4]);
            Assert.Equal((uint)615, sliceWatch[1, 5]);
            Assert.Equal((uint)616, sliceWatch[1, 6]);
            Assert.Equal((uint)617, sliceWatch[1, 7]);
            Assert.Equal((uint)618, sliceWatch[1, 8]);
            Assert.Equal((uint)619, sliceWatch[1, 9]);
        }
    }
}
