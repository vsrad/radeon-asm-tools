using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer.Wavemap;

namespace VSRAD.Package.Server
{
    // struct lane_data {
    // uint32_t system;
    // uint32_t watches[n_watches];
    // };
    // lane_data log_file[group_count][group_size]
    //
    // system[64] is spread across a wavefront (64 lanes)

    public sealed class WaveView
    {
        public int StartGlobalId { get; }
        public int StartThreadId { get; }
        public int EndThreadId { get; }
        public int WaveSize { get; }

        private readonly uint[] _data;
        private readonly IReadOnlyDictionary<uint, string[]> _instanceWatches;

        public WaveView(int groupThreadIdOffset, int startThreadId, int endThreadId, int waveSize, uint[] data, IReadOnlyDictionary<uint, string[]> instanceWatches)
        {
            StartGlobalId = groupThreadIdOffset + startThreadId;
            StartThreadId = startThreadId;
            EndThreadId = endThreadId;
            WaveSize = waveSize;
            _data = data;
            _instanceWatches = instanceWatches;
        }

        public uint GetSystemMagicNumber() => GetSystem().ElementAt(0);

        public uint GetSystemBreakpointLine() => GetSystem().ElementAt(1);

        public uint GetSystemInstanceId() => GetSystem().ElementAt(2);

        public uint GetSystemScc() => GetSystem().ElementAt(3);

        public uint[] GetSystemExecMask() => GetSystem().Skip(8).Take(2).ToArray();

        public IEnumerable<uint> GetSystem() => EnumerateWatchData(0);

        public IEnumerable<uint> GetWatchOrNull(string watch)
        {
            int watchIndex;
            if (_instanceWatches.TryGetValue(GetSystemInstanceId(), out var watches) && (watchIndex = Array.IndexOf(watches, watch)) != -1)
                return EnumerateWatchData(watchOffset: watchIndex + 1 /* skip System watch */);
            else
                return null;
        }

        private IEnumerable<uint> EnumerateWatchData(int watchOffset)
        {
            var dwordsPerLane = BreakStateData.GetDwordsPerLane(_instanceWatches);
            var waveStartOffset = StartGlobalId * dwordsPerLane;
            var waveEndOffset = (StartGlobalId + WaveSize) * dwordsPerLane;
            for (var offset = waveStartOffset + watchOffset; offset < waveEndOffset; offset += dwordsPerLane)
                yield return _data[offset];
        }
    }

    public sealed class SliceWatchView
    {
        public int ColumnCount { get; }
        public int RowCount { get; }

        private readonly int _laneDataOffset;
        private readonly int _laneDataSize;
        private readonly int _lastValidIndex;

        private readonly uint[] _data;

        public SliceWatchView(uint[] data, int groupsInRow, int groupSize, int groupCount, int laneDataOffset, int laneDataSize)
        {
            _data = data;
            _laneDataOffset = laneDataOffset;
            _laneDataSize = laneDataSize;
            _lastValidIndex = groupSize * groupCount * laneDataSize + _laneDataOffset;

            ColumnCount = groupsInRow * groupSize;
            RowCount = (_data.Length / _laneDataSize / ColumnCount) + groupCount % groupsInRow;
        }

        public bool IsInactiveCell(int row, int column)
        {
            var groupIdx = row * ColumnCount + column;
            var dwordIdx = groupIdx * _laneDataSize + _laneDataOffset;
            return dwordIdx > _lastValidIndex;
        }

        public uint this[int row, int column]
        {
            get
            {
                var groupIdx = row * ColumnCount + column;
                var dwordIdx = groupIdx * _laneDataSize + _laneDataOffset;
                return dwordIdx <= _lastValidIndex ? _data[dwordIdx] : 0;
            }
        }
    }

    public sealed class BreakStateData
    {
        public IReadOnlyDictionary<uint, string[]> InstanceWatches { get; }

        public int GroupIndex { get; private set; }
        public int GroupSize { get; private set; }
        public int WaveSize { get; private set; }

        private readonly BreakStateOutputFile _outputFile;

        private readonly uint[] _data;
        private readonly bool _localData;
        private BitArray _fetchedDataWaves; // 1 bit per wavefront data

        public BreakStateData(IReadOnlyDictionary<uint, string[]> instanceWatches, BreakStateOutputFile file, byte[] localData = null)
        {
            InstanceWatches = instanceWatches;
            _outputFile = file;

            _data = new uint[file.DwordCount];
            _localData = localData != null;
            if (_localData)
            {
                if (file.Offset != 0)
                    throw new ArgumentException("Trim the offset before passing output data to BreakStateData");
                Buffer.BlockCopy(localData, file.Offset, _data, 0, file.DwordCount * 4);
            }
        }

        public static int GetDwordsPerLane(IReadOnlyDictionary<uint, string[]> instanceWatches) =>
            1 /* System watch */ + (instanceWatches.Count == 0 ? 0 : instanceWatches.Max(i => i.Value.Length));

        public int GetGroupCount(int groupSize, int waveSize, int nGroups)
        {
            var realGroupSize = ((groupSize + waveSize - 1) / waveSize) * waveSize; // real group size is always a multiple of wave size
            var realCount = _data.Length / realGroupSize / GetDwordsPerLane(InstanceWatches);
            // Disabled for now as it should be refactored
            //if (nGroups != 0 && nGroups < realCount)
            //    return nGroups;
            return realCount;
        }

        public IEnumerable<WaveView> GetWaveViews()
        {
            var wavesPerGroup = (GroupSize + WaveSize - 1) / WaveSize;
            var groupThreadIdOffset = wavesPerGroup * GroupIndex * WaveSize;
            var groupSizeAvailable = Math.Min(GroupSize, (_data.Length - groupThreadIdOffset) / GetDwordsPerLane(InstanceWatches)); // group size can be set by user, so may exceed data bounds
            for (var startThreadId = 0; startThreadId < groupSizeAvailable; startThreadId += WaveSize)
            {
                var endThreadId = Math.Min(startThreadId + WaveSize, groupSizeAvailable);
                yield return new WaveView(groupThreadIdOffset, startThreadId, endThreadId, WaveSize, _data, InstanceWatches);
            }
        }

        public SliceWatchView GetSliceWatch(string watch, int groupsInRow, int nGroups)
        {
#if false
            int laneDataOffset;
            if (watch == "System")
            {
                laneDataOffset = 0;
            }
            else
            {
                var watchIndex = Watches.IndexOf(watch);
                if (watchIndex == -1)
                    return null;
                laneDataOffset = watchIndex + 1;
            }
            return new SliceWatchView(_data, groupsInRow, GroupSize, GetGroupCount(GroupSize, WaveSize, nGroups), laneDataOffset, _dwordsPerLane);
#else
            throw new NotImplementedException();
#endif
        }

        public WavemapView GetWavemapView()
        {
            if (GroupSize > 0 && WaveSize > 0)
                return new WavemapView(_data, WaveSize, GetDwordsPerLane(InstanceWatches), GroupSize, GetGroupCount(GroupSize, WaveSize, 0));
            return null;
        }

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int waveSize, int nGroups, bool fetchWholeFile = false)
        {
            if (waveSize != WaveSize)
            {
                var waveDataSize = waveSize * GetDwordsPerLane(InstanceWatches);
                if (!_localData)
                    _fetchedDataWaves = new BitArray((_data.Length + waveDataSize - 1) / waveDataSize, false);
                WaveSize = waveSize;
            }

            string warning = null;
            if (!_localData)
                warning = await FetchFilePartAsync(channel, groupIndex, groupSize, nGroups, fetchWholeFile);

            GroupIndex = groupIndex;
            GroupSize = groupSize;
            return warning;
        }

        private async Task<string> FetchFilePartAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int nGroups, bool fetchWholeFile)
        {
            GetRequestedFilePart(groupIndex, groupSize, nGroups, fetchWholeFile, out var waveOffset, out var waveCount);
            if (IsFilePartFetched(waveOffset, waveCount))
                return null;

            var dwordsPerLane = GetDwordsPerLane(InstanceWatches);
            var waveDataSize = dwordsPerLane * WaveSize;
            var requestedByteOffset = waveOffset * waveDataSize * 4;
            var requestedByteCount = Math.Min(waveCount * waveDataSize, _outputFile.DwordCount) * 4;

            var response = await channel.SendWithReplyAsync<DebugServer.IPC.Responses.ResultRangeFetched>(
                new DebugServer.IPC.Commands.FetchResultRange
                {
                    FilePath = _outputFile.Path,
                    BinaryOutput = _outputFile.BinaryOutput,
                    ByteOffset = requestedByteOffset,
                    ByteCount = requestedByteCount,
                    OutputOffset = _outputFile.Offset
                }).ConfigureAwait(false);

            if (response.Status != DebugServer.IPC.Responses.FetchStatus.Successful)
                return "Output file could not be opened.";

            Buffer.BlockCopy(response.Data, 0, _data, requestedByteOffset, response.Data.Length);
            var fetchedWaveCount = response.Data.Length / waveDataSize / 4;
            MarkFilePartAsFetched(waveOffset, fetchedWaveCount);

            if (response.Timestamp != _outputFile.Timestamp)
                return "Output file has changed since the last debugger execution.";

            if (response.Data.Length < requestedByteCount)
                return $"Group #{groupIndex} is incomplete: expected to read {requestedByteCount} bytes but the output file contains {response.Data.Length}.";

            if (_data.Length < nGroups * groupSize * dwordsPerLane)
                return $"Output file has fewer groups than requested (NGroups = {nGroups}, but the file contains only {GetGroupCount(groupSize, WaveSize, nGroups)})";

            return null;
        }

        private void GetRequestedFilePart(int groupIndex, int groupSize, int nGroups, bool fetchWholeFile, out int waveOffset, out int waveCount)
        {
            groupSize = ((groupSize + WaveSize - 1) / WaveSize) * WaveSize; // real group size is always a multiple of wave size
            if (fetchWholeFile)
            {
                waveCount = nGroups * groupSize / WaveSize;
                if (waveCount == 0 || waveCount > _fetchedDataWaves.Length)
                    waveCount = _fetchedDataWaves.Length;
                waveOffset = 0;
            }
            else // single group
            {
                var groupStart = groupIndex * groupSize;
                var groupEnd = (groupIndex + 1) * groupSize;
                var startWaveIndex = groupStart / WaveSize;
                var endWaveIndex = groupEnd / WaveSize + (groupEnd % WaveSize != 0 ? 1 : 0); // ceil

                waveCount = endWaveIndex - startWaveIndex;
                waveOffset = startWaveIndex;
            }
        }

        private bool IsFilePartFetched(int waveOffset, int waveCount)
        {
            for (int i = waveOffset; i < waveOffset + waveCount; ++i)
                if (!_fetchedDataWaves[i])
                    return false;
            return true;
        }

        private void MarkFilePartAsFetched(int waveOffset, int waveCount)
        {
            for (int i = waveOffset; i < waveOffset + waveCount; ++i)
                _fetchedDataWaves[i] = true;
        }
    }
}
