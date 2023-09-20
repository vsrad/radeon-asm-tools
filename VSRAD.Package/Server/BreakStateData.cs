using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public IReadOnlyDictionary<string, WatchMeta> Watches { get; }
        public int DwordsPerLane { get; }
        public int NumThreadsInProgram { get; }

        public int GroupIndex { get; private set; }
        public int GroupSize { get; private set; }
        public int WaveSize { get; private set; }
        public int WavesPerGroup => MathUtils.RoundUpQuotient(GroupSize, WaveSize);

        private readonly BreakStateOutputFile _outputFile;

        private readonly uint[] _data;
        private readonly bool _localData;
        private BitArray _fetchedDataWaves; // 1 bit per wavefront data

        private static readonly Regex _watchIndexRegex = new Regex(@"\[(\d+)\]$", RegexOptions.Compiled);

        public BreakStateData(IReadOnlyDictionary<string, WatchMeta> watches, int dwordsPerLane, BreakStateOutputFile file, byte[] localData = null)
        {
            Watches = watches;
            DwordsPerLane = dwordsPerLane;
            NumThreadsInProgram = file.DwordCount / dwordsPerLane;
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

        public WatchMeta GetWatchMeta(string watch)
        {
            if (Watches.TryGetValue(watch, out var watchMeta))
                return watchMeta;

            if (_watchIndexRegex.Match(watch) is Match indexMatch && indexMatch.Success)
            {
                var parentWatch = watch.Substring(0, indexMatch.Groups[0].Index);
                var idx = uint.Parse(indexMatch.Groups[1].Value);
                if (GetWatchMeta(parentWatch) is WatchMeta parent && idx < parent.ListItems.Count)
                    return parent.ListItems[(int)idx];
            }

            return null;
        }

        /// <returns>An array of exactly n=WaveSize elements, with 0s for missing data</returns>
        public uint[] GetWatchData(int waveIndex, int dataSlot)
        {
            if (waveIndex >= WavesPerGroup)
                throw new ArgumentOutOfRangeException(nameof(waveIndex), waveIndex, $"Wave index must be less than the number of waves per group ({WavesPerGroup})");
            if (dataSlot >= DwordsPerLane)
                throw new ArgumentOutOfRangeException(nameof(dataSlot), dataSlot, $"Data slot must be less than the number of dwords per lane ({DwordsPerLane})");

            var startThreadId = (WavesPerGroup * GroupIndex + waveIndex) * WaveSize;
            var dataStart = startThreadId * DwordsPerLane + dataSlot;
            var dataEnd = Math.Min((startThreadId + WaveSize) * DwordsPerLane, _data.Length);

            var watchData = new uint[WaveSize];
            for (int i = 0, offset = dataStart; offset < dataEnd; i += 1, offset += DwordsPerLane)
                watchData[i] = _data[offset];
            return watchData;
        }

        public uint[] GetSystemData(int waveIndex) => GetWatchData(waveIndex, 0);

        public const int SystemMagicNumberLane = 0;
        public const int SystemBreakLineLane = 1;
        public const int SystemInstanceIdLane = 2;
        public const int SystemSccLane = 3;
        public const int SystemExecLoLane = 8;
        public const int SystemExecHiLane = 9;

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
                return new WavemapView(_data, WaveSize, DwordsPerLane, GroupSize, GetGroupCount(GroupSize, WaveSize, 0));
            return null;
        }

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int waveSize, bool fetchWholeFile = false)
        {
            if (waveSize != WaveSize)
            {
                var waveDataSize = waveSize * DwordsPerLane;
                if (!_localData)
                    _fetchedDataWaves = new BitArray(MathUtils.RoundUpQuotient(_data.Length, waveDataSize), false);
                WaveSize = waveSize;
            }

            string warning = null;
            if (!_localData)
                warning = await FetchFilePartAsync(channel, groupIndex, groupSize, fetchWholeFile);

            GroupIndex = groupIndex;
            GroupSize = groupSize;
            return warning;
        }

        private async Task<string> FetchFilePartAsync(ICommunicationChannel channel, int groupIndex, int groupSize, bool fetchWholeFile)
        {
            GetRequestedFilePart(groupIndex, groupSize, fetchWholeFile, out var waveOffset, out var waveCount);
            if (IsFilePartFetched(waveOffset, waveCount))
                return null;

            var waveDataSize = DwordsPerLane * WaveSize;
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

            return null;
        }

        private void GetRequestedFilePart(int groupIndex, int groupSize, bool fetchWholeFile, out int waveOffset, out int waveCount)
        {
            groupSize = MathUtils.RoundUpToMultiple(groupSize, WaveSize); // real group size is always a multiple of wave size
            if (fetchWholeFile)
            {
                waveCount = MathUtils.RoundUpQuotient(NumThreadsInProgram, WaveSize);
                if (waveCount == 0 || waveCount > _fetchedDataWaves.Length)
                    waveCount = _fetchedDataWaves.Length;
                waveOffset = 0;
            }
            else // single group
            {
                var groupStart = groupIndex * groupSize;
                var groupEnd = (groupIndex + 1) * groupSize;
                var startWaveIndex = groupStart / WaveSize;
                var endWaveIndex = MathUtils.RoundUpQuotient(groupEnd, WaveSize);

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
