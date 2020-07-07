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
                file: new Package.Options.OutputFile("/home/kyubey/projects", "madoka", true),
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
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group1Bin },
                (command) =>
                {
                    Assert.Equal(0, command.ByteOffset);
                    Assert.Equal(2048, command.ByteCount);
                });
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
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = group2Bin },
                (command) =>
                {
                    Assert.Equal(2048, command.ByteOffset);
                    Assert.Equal(2048, command.ByteCount);
                });
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
        public async Task UnevenGroupSizeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var breakStateData = new BreakStateData(
                watches: new ReadOnlyCollection<string>(new[] { "const" }),
                file: new Package.Options.OutputFile("/home/kyubey/projects", "madoka", true),
                fileTimestamp: default,
                outputByteCount: 2 * 256 * sizeof(int),
                outputOffset: 0);

            var data = new int[2 * 256];
            for (int i = 0; i < 256; ++i)
            {
                data[2 * i + 0] = i; // system = global id
                data[2 * i + 1] = 777; // first watch = const
            }

            // Assuming a group size of 65, the second group (65-129) requires two waves, 64-127 and 128-191, i.e. byte offset (2 watches)*64*4 and byte cound (2 watches)*(2 waves)*64*4

            var requestedData = new byte[sizeof(int) * 128 * 2];
            Buffer.BlockCopy(data, sizeof(int) * 64 * 2, requestedData, 0, requestedData.Length);
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = requestedData },
                (command) =>
                {
                    Assert.Equal(2 * 64 * 4, command.ByteOffset);
                    Assert.Equal(2 * 2 * 64 * 4, command.ByteCount);
                });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 1, groupSize: 65, nGroups: 2);
            Assert.Null(warning);

            var system = breakStateData.GetSystem();
            for (var i = 0; i < 65; ++i)
                Assert.Equal(65 + i, (int)system[i]);
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


        [Fact]
        public void SliceWatchViewTest()
        {
            var data = new uint[] { 600, 0, 601, 10, 602, 20, 603, 30, 604, 40, 605, 1, 606,
                                    11, 607, 21, 608, 31, 609, 41, 610, 2, 611, 12, 612, 22, 613, 32, 614, 42, 615, 3, 616, 13,
                                    617, 23, 618, 33, 619, 43 };

            var sliceWatch = new SliceWatchView(data, groupsInRow: 2, groupSize: 5, groupCount: 4, laneDataOffset: 1, laneDataSize: 2);
            var expected = new uint[,] { { 0, 10, 20, 30, 40, 1, 11, 21, 31, 41 },
                                         { 2, 12, 22, 32, 42, 3, 13, 23, 33, 43 } };

            for (int row = 0; row < 2; ++row)
                for (int col = 0; col < 10; ++col)
                    Assert.Equal(expected[row, col], sliceWatch[row, col]);

            sliceWatch = new SliceWatchView(data, groupsInRow: 2, groupSize: 5, groupCount: 4, laneDataOffset: 0, laneDataSize: 2);
            expected = new uint[,] { { 600, 601, 602, 603, 604, 605, 606, 607, 608, 609 },
                                     { 610, 611, 612, 613, 614, 615, 616, 617, 618, 619 } };

            for (int row = 0; row < 2; ++row)
                for (int col = 0; col < 10; ++col)
                    Assert.Equal(expected[row, col], sliceWatch[row, col]);
        }
    }
}
