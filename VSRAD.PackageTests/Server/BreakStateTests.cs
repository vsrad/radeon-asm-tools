using System;
using System.Collections.Generic;
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
            var watches = new ReadOnlyCollection<string>(new[] { "local_id", "group_id", "group_size" });
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "madoka" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakStateData = new BreakStateData(watches, file);

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
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0, groupSize: 128, waveSize: 64, nGroups: 2);
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
            warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 1, groupSize: 128, waveSize: 64, nGroups: 2);
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
            warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 1, groupSize: 64, waveSize: 64, nGroups: 2);
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
        public async Task IncompleteGroupTestAsync() /* Incomplete group: group size % wave size != 0 */
        {
            var waveSize = 64;
            var groupSize = 65;
            var groupCount = 3;
            var paddedGroupSize = 128; // two waves per group
            var laneCount = 384; // groupCount * paddedGroupSize
            var watchCount = 2; // system + 1 watch

            var channel = new MockCommunicationChannel();
            var watches = new ReadOnlyCollection<string>(new[] { "local_id" });
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "madoka" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: watchCount * laneCount);
            var breakStateData = new BreakStateData(watches, file);

            Assert.Equal(groupCount, breakStateData.GetGroupCount(groupSize, waveSize, 0));

            var data = new int[watchCount * laneCount];
            for (int i = 0; i < laneCount; ++i)
            {
                data[watchCount * i + 0] = i; // system = global id
                data[watchCount * i + 1] = i % paddedGroupSize; // first watch = local id
            }

            // Group #2 starts at 256
            var requestedGroupIndex = 2;

            var requestedData = new byte[watchCount * paddedGroupSize * sizeof(int)];
            Buffer.BlockCopy(data, watchCount * paddedGroupSize * requestedGroupIndex * sizeof(int), requestedData, 0, requestedData.Length);
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = requestedData },
                (command) =>
                {
                    Assert.Equal(2 * 128 * 2 * 4, command.ByteOffset);
                    Assert.Equal(2 * 128 * 4, command.ByteCount);
                });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: requestedGroupIndex, groupSize: groupSize, waveSize: waveSize, nGroups: groupCount);
            Assert.Null(warning);

            var system = breakStateData.GetSystem();
            var watch = breakStateData.GetWatch("local_id");
            for (var i = 0; i < groupSize; ++i)
            {
                Assert.Equal(paddedGroupSize * requestedGroupIndex + i, (int)system[i]);
                Assert.Equal(i, (int)watch[i]);
            }
        }

        [Fact]
        public async Task EmptyResultRangeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var watches = new ReadOnlyCollection<string>(new[] { "h" });
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "log.tar" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakStateData = new BreakStateData(watches, file);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>() },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects", "log.tar" }, command.FilePath);
            });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0, groupSize: 512, waveSize: 64, nGroups: 1);
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", warning);
            // Data is set to 0 if unavailable
            Assert.Equal(0u, breakStateData.GetSystem()[0]);
        }

        [Fact]
        public async Task NGroupViolationProducesAWarningButFetchesResultsTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var watches = new ReadOnlyCollection<string>(new[] { "h" });
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "log.tar" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakStateData = new BreakStateData(watches, file);

            Assert.Equal(2, breakStateData.GetGroupCount(groupSize: 256, waveSize: 64, nGroups: 4));

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = new byte[2048] }, (_) => { });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0, groupSize: 256, waveSize: 64, nGroups: 4);

            Assert.Equal("Output file has fewer groups than requested (NGroups = 4, but the file contains only 2)", warning);
        }

        [Fact]
        public async Task BreakStateWithLocalDataTestAsync()
        {
            var data = new int[2 * 256];
            for (int i = 0; i < 256; ++i)
            {
                data[2 * i + 0] = i; // system = global id
                data[2 * i + 1] = i % 32; // first watch = local id
            }

            var localData = new byte[2 * 256 * sizeof(int)];
            Buffer.BlockCopy(data, 0, localData, 0, localData.Length);

            var watches = new ReadOnlyCollection<string>(new[] { "local_id" });
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "madoka" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 2 * 256);
            var breakStateData = new BreakStateData(watches, file, localData);

            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel: null, groupIndex: 1, groupSize: 64, waveSize: 32, nGroups: 2);
            Assert.Null(warning);

            var system = breakStateData.GetSystem();
            var localIdWatch = breakStateData.GetWatch("local_id");
            for (var i = 0; i < 64; ++i)
            {
                Assert.Equal(64 + i, (int)system[i]);
                Assert.Equal(i % 32, (int)localIdWatch[i]);
            }
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
