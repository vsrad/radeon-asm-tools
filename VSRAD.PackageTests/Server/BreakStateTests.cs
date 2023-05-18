using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateTests
    {
        [Theory]
        [InlineData(2, 64, 32)]
        [InlineData(2, 128, 64)]
        [InlineData(2, 96, 64)] // Incomplete group: group size % wave size != 0
        public async Task WaveViewTestAsync(int groupCount, int groupSize, int waveSize)
        {
            var channel = new MockCommunicationChannel();
            var instanceWatches = new Dictionary<uint, string[]> {
                { 0, new[] { "ThreadID", "GlobalID", "GlobalID<<2" } },
                { 1, new[] { "GlobalID<<8", "GlobalID<<4", "GlobalID", "ThreadID" } },
            };
            var dwordsPerLane = 5; // max watches per instance + 1
            var wavesPerGroup = (groupSize + waveSize - 1) / waveSize;
            var dwordsPerGroup = wavesPerGroup * waveSize * dwordsPerLane;
            var dwordsInBuffer = groupCount * dwordsPerGroup;

            var file = new BreakStateOutputFile(new[] { "/working/dir", "debug_buffer.bin" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: dwordsInBuffer);
            var breakStateData = new BreakStateData(instanceWatches, file);
            Assert.Equal(dwordsPerLane, BreakStateData.GetDwordsPerLane(instanceWatches));

            var data = new int[dwordsInBuffer];
            for (int group = 0; group < groupCount; ++group)
            {
                for (int wave = 0; wave < wavesPerGroup; ++wave)
                {
                    for (int lane = 0; lane < waveSize; ++lane)
                    {
                        int instanceId = group; // group 0 is instance 0, group 1 is instance 1
                        int globalId = (group * wavesPerGroup + wave) * waveSize + lane;
                        int threadId = wave * waveSize + lane;
                        int laneDataOffset = dwordsPerLane * globalId;
                        data[laneDataOffset + 0] = instanceId; // system = instance id
                        switch (instanceId)
                        {
                            case 0:
                                data[laneDataOffset + 1] = threadId;
                                data[laneDataOffset + 2] = globalId;
                                data[laneDataOffset + 3] = globalId << 2;
                                break;
                            case 1:
                                data[laneDataOffset + 1] = globalId << 8;
                                data[laneDataOffset + 2] = globalId << 4;
                                data[laneDataOffset + 3] = globalId;
                                data[laneDataOffset + 4] = threadId;
                                break;
                        }
                    }
                }
            }
            for (int group = 0; group < groupCount; ++group)
            {
                var groupByteSize = sizeof(int) * dwordsPerGroup;
                var groupBin = new byte[groupByteSize];
                Buffer.BlockCopy(data, group * groupByteSize, groupBin, 0, groupByteSize);
                channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = groupBin },
                    (command) =>
                    {
                        Assert.Equal(group * groupByteSize, command.ByteOffset);
                        Assert.Equal(groupByteSize, command.ByteCount);
                    });
                var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: group, groupSize: groupSize, waveSize: waveSize, nGroups: groupCount);
                Assert.Null(warning);

                var waves = breakStateData.GetWaveViews().ToArray();
                Assert.Equal(wavesPerGroup, waves.Length);
                for (var wave = 0; wave < wavesPerGroup; ++wave)
                {
                    Assert.Equal(waveSize, waves[wave].WaveSize);
                    Assert.Equal(wave * waveSize, waves[wave].StartThreadId);
                    Assert.Equal(Math.Min((wave + 1) * waveSize, groupSize), waves[wave].EndThreadId);
                    for (var lane = 0; lane < waveSize; ++lane)
                    {
                        int globalId = (group * wavesPerGroup + wave) * waveSize + lane, threadId = wave * waveSize + lane;

                        var systemData = waves[wave].GetSystem().ToArray();
                        Assert.Equal((uint)group, systemData[lane]);

                        var globalIdData = waves[wave].GetWatchOrNull("GlobalID").ToArray();
                        Assert.Equal((uint)globalId, globalIdData[lane]);

                        var threadIdData = waves[wave].GetWatchOrNull("ThreadID").ToArray();
                        Assert.Equal((uint)threadId, threadIdData[lane]);

                        switch (group)
                        {
                            case 0:
                                var globalIdShl2Data = waves[wave].GetWatchOrNull("GlobalID<<2").ToArray();
                                Assert.Equal((uint)globalId << 2, globalIdShl2Data[lane]);

                                Assert.Null(waves[wave].GetWatchOrNull("GlobalID<<4"));

                                Assert.Null(waves[wave].GetWatchOrNull("GlobalID<<8"));
                                break;
                            case 1:
                                Assert.Null(waves[wave].GetWatchOrNull("GlobalID<<2"));

                                var globalIdShl4Data = waves[wave].GetWatchOrNull("GlobalID<<4").ToArray();
                                Assert.Equal((uint)globalId << 4, globalIdShl4Data[lane]);

                                var globalIdShl8Data = waves[wave].GetWatchOrNull("GlobalID<<8").ToArray();
                                Assert.Equal((uint)globalId << 8, globalIdShl8Data[lane]);
                                break;
                        }
                    }
                }
            }
            {
                // Switching to a smaller group that was already fetched doesn't send any requests
                var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 1, groupSize: groupSize / 2, waveSize: waveSize, nGroups: wavesPerGroup);
                Assert.Null(warning);
            }
        }

        [Fact]
        public async Task EmptyResultRangeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var instanceWatches = new Dictionary<uint, string[]> { { 0, new[] { "ThreadID" } } };
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "log.tar" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakStateData = new BreakStateData(instanceWatches, file);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>() },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects", "log.tar" }, command.FilePath);
            });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0, groupSize: 512, waveSize: 64, nGroups: 1);
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", warning);
            // Data is set to 0 if unavailable
            foreach (var wave in breakStateData.GetWaveViews())
            {
                for (var i = 0; i < breakStateData.WaveSize; ++i)
                {
                    Assert.Equal(0u, wave.GetSystem().ElementAt(i));
                    Assert.Equal(0u, wave.GetWatchOrNull("ThreadID").ElementAt(i));
                }
            }
        }

        [Fact]
        public async Task NGroupViolationProducesAWarningButFetchesResultsTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var instanceWatches = new Dictionary<uint, string[]> { { 0, new[] { "ThreadID" } } };
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "log.tar" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakStateData = new BreakStateData(instanceWatches, file);

            Assert.Equal(2, breakStateData.GetGroupCount(groupSize: 256, waveSize: 64, nGroups: 4));

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = new byte[2048] }, (_) => { });
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0, groupSize: 256, waveSize: 64, nGroups: 4);

            Assert.Equal("Output file has fewer groups than requested (NGroups = 4, but the file contains only 2)", warning);
        }

        [Fact]
        public async Task BreakStateWithLocalDataTestAsync()
        {
            int instanceId = 123456;
            int waveSize = 32, groupSize = 64, nGroups = 2, dwordsPerLane = 3;

            var data = new int[groupSize * nGroups * dwordsPerLane];
            for (var gid = 0; gid < nGroups; ++gid)
            {
                for (var tid = 0; tid < groupSize; ++tid)
                {
                    data[(gid * groupSize + tid) * dwordsPerLane + 0] = instanceId;
                    data[(gid * groupSize + tid) * dwordsPerLane + 1] = gid * groupSize + tid;
                    data[(gid * groupSize + tid) * dwordsPerLane + 2] = tid;
                }
            }

            var localData = new byte[data.Length * sizeof(int)];
            Buffer.BlockCopy(data, 0, localData, 0, localData.Length);

            var instanceWatches = new Dictionary<uint, string[]> { { (uint)instanceId, new[] { "GlobalID", "ThreadID" } } };
            var file = new BreakStateOutputFile(new[] { "/home/kyubey/projects", "log.tar" }, binaryOutput: true, offset: 0, timestamp: default, dwordCount: data.Length);
            var breakStateData = new BreakStateData(instanceWatches, file, localData);

            var groupId = 1;
            var warning = await breakStateData.ChangeGroupWithWarningsAsync(channel: null, groupIndex: groupId, groupSize: groupSize, waveSize: waveSize, nGroups: nGroups);
            Assert.Null(warning);

            for (var tid = 0; tid < groupSize; ++tid)
            {
                var wave = breakStateData.GetWaveViews().ElementAt(tid / waveSize);
                Assert.Equal(instanceId, (int)wave.GetSystem().ElementAt(tid % waveSize));
                Assert.Equal(groupId * groupSize + tid, (int)wave.GetWatchOrNull("GlobalID").ElementAt(tid % waveSize));
                Assert.Equal(tid, (int)wave.GetWatchOrNull("ThreadID").ElementAt(tid % waveSize));
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
