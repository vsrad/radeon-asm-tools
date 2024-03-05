using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class BreakStateTests
    {
        private static BreakState MakeBreakState(Dictionary<string, WatchMeta> watches, uint dwordsPerLane, BreakStateOutputFile file, uint groupSize, uint waveSize, byte[] localData = null)
        {
            var breakState = new BreakState(BreakTarget.Empty, watches,
                new BreakStateDispatchParameters(waveSize: waveSize, gridX: groupSize, gridY: 1, gridZ: 1, groupX: groupSize, groupY: 1, groupZ: 1, ""),
                new Dictionary<uint, uint>(), dwordsPerLane: dwordsPerLane, file, checkMagicNumber: null, localData);
            return breakState;
        }

        [Theory]
        [InlineData(2, 64, 32)]
        [InlineData(2, 128, 64)]
        [InlineData(2, 96, 64)] // Incomplete group: group size % wave size != 0
        public async Task WaveViewTestAsync(uint groupCount, uint groupSize, uint waveSize)
        {
            var channel = new MockCommunicationChannel();
            var watches = new Dictionary<string, WatchMeta> {
                { "ThreadID", new WatchMeta(new[] { (Instance: 0u, DataSlot: (uint?)1, (uint?)null), (Instance: 1u, DataSlot: (uint?)4, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
                { "GlobalID", new WatchMeta(new[] { (Instance: 0u, DataSlot: (uint?)2, (uint?)null), (Instance: 1u, DataSlot: (uint?)3, (uint?)null), }, Enumerable.Empty<WatchMeta>()) },
                { "GlobalID<<2", new WatchMeta(new[] { (Instance: 0u, DataSlot: (uint?)3, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
                { "GlobalID<<4", new WatchMeta(new[] { (Instance: 1u, DataSlot: (uint?)2, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
                { "GlobalID<<8", new WatchMeta(new[] { (Instance: 1u, DataSlot: (uint?)1, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
            };
            var dwordsPerLane = 5u; // max watches per instance + 1
            var wavesPerGroup = (groupSize + waveSize - 1) / waveSize;
            var dwordsPerGroup = wavesPerGroup * waveSize * dwordsPerLane;
            var dwordsInBuffer = groupCount * dwordsPerGroup;

            var file = new BreakStateOutputFile("/working/dir/debug_buffer.bin", binaryOutput: true, offset: 0, timestamp: default, dwordCount: (int)dwordsInBuffer);
            var breakState = MakeBreakState(watches, dwordsPerLane, file, groupSize, waveSize);

            var data = new uint[dwordsInBuffer];
            for (uint group = 0; group < groupCount; ++group)
            {
                for (uint wave = 0; wave < wavesPerGroup; ++wave)
                {
                    for (uint lane = 0; lane < waveSize; ++lane)
                    {
                        uint instanceId = group; // group 0 is instance 0, group 1 is instance 1
                        uint globalId = (group * wavesPerGroup + wave) * waveSize + lane;
                        uint threadId = wave * waveSize + lane;
                        uint laneDataOffset = dwordsPerLane * globalId;
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
            for (uint group = 0; group < groupCount; ++group)
            {
                var groupByteSize = sizeof(uint) * dwordsPerGroup;
                var groupBin = new byte[groupByteSize];
                Buffer.BlockCopy(data, (int)(group * groupByteSize), groupBin, 0, (int)groupByteSize);
                channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = groupBin },
                    (command) =>
                    {
                        Assert.Equal(group * groupByteSize, (uint)command.ByteOffset);
                        Assert.Equal(groupByteSize, (uint)command.ByteCount);
                    });
                var warning = await breakState.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: group);
                Assert.Null(warning);

                Assert.Equal(wavesPerGroup, breakState.WavesPerGroup);
                for (uint wave = 0; wave < wavesPerGroup; ++wave)
                {
                    for (uint lane = 0, threadId = wave * waveSize; lane < waveSize; ++lane, ++threadId)
                    {
                        var globalId = (group * wavesPerGroup + wave) * waveSize + lane;

                        var systemData = breakState.GetSystemData(wave);
                        Assert.Equal(group, systemData[lane]);

                        Assert.True(breakState.Watches.TryGetValue("GlobalID", out var globalIdWatch));
                        var (_, globalIdWatchSlot, _) = globalIdWatch.Instances.Find(v => v.Instance == group);
                        var globalIdData = breakState.GetWatchData(wave, (uint)globalIdWatchSlot);
                        Assert.Equal(globalId, globalIdData[lane]);

                        Assert.True(breakState.Watches.TryGetValue("ThreadID", out var threadIdWatch));
                        var (_, threadIdWatchSlot, _) = threadIdWatch.Instances.Find(v => v.Instance == group);
                        var threadIdData = breakState.GetWatchData(wave, (uint)threadIdWatchSlot);
                        Assert.Equal(threadId, threadIdData[lane]);

                        switch (group)
                        {
                            case 0:
                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<2", out var globalIdShl2Watch));
                                var (_, globalIdShl2Slot, _) = globalIdShl2Watch.Instances.Find(v => v.Instance == group);
                                var globalIdShl2Data = breakState.GetWatchData(wave, (uint)globalIdShl2Slot);
                                Assert.Equal(globalId << 2, globalIdShl2Data[lane]);

                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<4", out var globalIdShl4Watch));
                                Assert.False(globalIdShl4Watch.Instances.Exists(v => v.Instance == group));

                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<8", out var globalIdShl8Watch));
                                Assert.False(globalIdShl8Watch.Instances.Exists(v => v.Instance == group));
                                break;
                            case 1:
                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<2", out globalIdShl2Watch));
                                Assert.False(globalIdShl2Watch.Instances.Exists(v => v.Instance == group));

                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<4", out globalIdShl4Watch));
                                var (_, globalIdShl4Slot, _) = globalIdShl4Watch.Instances.Find(v => v.Instance == group);
                                var globalIdShl4Data = breakState.GetWatchData(wave, (uint)globalIdShl4Slot);
                                Assert.Equal(globalId << 4, globalIdShl4Data[lane]);

                                Assert.True(breakState.Watches.TryGetValue("GlobalID<<8", out globalIdShl8Watch));
                                var (_, globalIdShl8Slot, _) = globalIdShl8Watch.Instances.Find(v => v.Instance == group);
                                var globalIdShl8Data = breakState.GetWatchData(wave, (uint)globalIdShl8Slot);
                                Assert.Equal(globalId << 8, globalIdShl8Data[lane]);
                                break;
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task EmptyResultRangeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var watches = new Dictionary<string, WatchMeta> { { "ThreadID", new WatchMeta(new[] { (Instance: 0u, DataSlot: (uint?)1, (uint?)null) }, Enumerable.Empty<WatchMeta>()) } };
            var file = new BreakStateOutputFile("/home/kyubey/projects/log.tar", binaryOutput: true, offset: 0, timestamp: default, dwordCount: 1024);
            var breakState = MakeBreakState(watches, dwordsPerLane: 2, file, groupSize: 512, waveSize: 64);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>() },
            (command) =>
            {
                Assert.Equal(new[] { "/home/kyubey/projects/log.tar" }, command.FilePath);
            });
            var warning = await breakState.ChangeGroupWithWarningsAsync(channel.Object, groupIndex: 0);
            Assert.Equal("Group #0 is incomplete: expected to read 4096 bytes but the output file contains 0.", warning);
            // Data is set to 0 if unavailable
            for (uint wave = 0; wave < breakState.WavesPerGroup; ++wave)
            {
                for (uint lane = 0, threadId = wave * breakState.Dispatch.WaveSize; lane < breakState.Dispatch.WaveSize; ++lane, ++threadId)
                {
                    Assert.Equal(0u, breakState.GetSystemData(wave)[lane]);

                    Assert.True(breakState.Watches.TryGetValue("ThreadID", out var threadIdWatch));
                    var (_, threadIdWatchSlot, _) = threadIdWatch.Instances.Find(v => v.Instance == 0);
                    Assert.Equal(0u, breakState.GetWatchData(wave, (uint)threadIdWatchSlot)[lane]);
                }
            }
        }

        [Fact]
        public async Task BreakStateWithLocalDataTestAsync()
        {
            uint instanceId = 123456;
            uint waveSize = 32, groupSize = 64, nGroups = 2, dwordsPerLane = 3;

            var data = new uint[groupSize * nGroups * dwordsPerLane];
            for (uint gid = 0; gid < nGroups; ++gid)
            {
                for (uint tid = 0; tid < groupSize; ++tid)
                {
                    data[(gid * groupSize + tid) * dwordsPerLane + 0] = instanceId;
                    data[(gid * groupSize + tid) * dwordsPerLane + 1] = gid * groupSize + tid;
                    data[(gid * groupSize + tid) * dwordsPerLane + 2] = tid;
                }
            }

            var localData = new byte[data.Length * sizeof(uint)];
            Buffer.BlockCopy(data, 0, localData, 0, localData.Length);

            var watches = new Dictionary<string, WatchMeta> {
                { "GlobalID", new WatchMeta(new[] { (Instance: instanceId, DataSlot: (uint?)1, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
                { "ThreadID", new WatchMeta(new[] { (Instance: instanceId, DataSlot: (uint?)2, (uint?)null) }, Enumerable.Empty<WatchMeta>()) },
            };
            var file = new BreakStateOutputFile("/home/kyubey/projects/log.tar", binaryOutput: true, offset: 0, timestamp: default, dwordCount: data.Length);
            var breakState = MakeBreakState(watches, dwordsPerLane: 3, file, groupSize, waveSize, localData);

            uint groupId = 1;
            var warning = await breakState.ChangeGroupWithWarningsAsync(channel: null, groupIndex: groupId);
            Assert.Null(warning);

            for (uint tid = 0; tid < groupSize; ++tid)
            {
                uint wave = tid / waveSize, lane = tid % waveSize;
                Assert.Equal(instanceId, breakState.GetSystemData(wave)[lane]);

                Assert.True(breakState.Watches.TryGetValue("ThreadID", out var threadIdWatch));
                var (_, threadIdWatchSlot, _) = threadIdWatch.Instances.Find(v => v.Instance == instanceId);
                Assert.Equal(tid, breakState.GetWatchData(wave, (uint)threadIdWatchSlot)[lane]);

                Assert.True(breakState.Watches.TryGetValue("GlobalID", out var globalIdWatch));
                var (_, globalIdWatchSlot, _) = globalIdWatch.Instances.Find(v => v.Instance == instanceId);
                Assert.Equal(groupId * groupSize + tid, breakState.GetWatchData(wave, (uint)globalIdWatchSlot)[lane]);
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
