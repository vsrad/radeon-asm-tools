using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public sealed class WatchView
    {
        private readonly int _startOffset;
        private readonly int _laneDataSize;

        private readonly uint[] _data;

        public int LastIndexInGroup { get; }

        public WatchView(uint[] data, int groupIndex, int displayGroupSize, int dataGroupSize, int laneDataOffset, int laneDataSize)
        {
            _data = data;
            var groupOffset = groupIndex * dataGroupSize * laneDataSize;
            _startOffset = groupOffset + laneDataOffset;
            _laneDataSize = laneDataSize;

            // Handle cases when some of the items in the last group are for some reason out of the bounds of data
            var itemsInGroup = (_data.Length - groupOffset) / laneDataSize;
            LastIndexInGroup = Math.Min(displayGroupSize, itemsInGroup) - 1;
        }

        public uint this[int index]
        {
            get
            {
                if (index > LastIndexInGroup)
                    throw new KeyNotFoundException($"Item #{index} in group requested, but the last valid index in the watch view is {LastIndexInGroup}.");

                var dwordIdx = _startOffset + index * _laneDataSize;
                return _data[dwordIdx];
            }
        }
    }

    public sealed class SliceWatchView
    {
        public int ColumnCount { get; }
        public int RowCount { get; }

        private readonly int _laneDataOffset;
        private readonly int _laneDataSize;
        private readonly int _lastValidIndex;
        private readonly int _groupsInRow;
        private readonly int _groupSize;

        private readonly uint[] _data;

        public SliceWatchView(uint[] data, int groupsInRow, int groupSize, int groupCount, int laneDataOffset, int laneDataSize)
        {
            _data = data;
            _laneDataOffset = laneDataOffset;
            _laneDataSize = laneDataSize;
            _lastValidIndex = groupSize * groupCount * laneDataSize + _laneDataOffset;
            _groupsInRow = groupsInRow;
            _groupSize = groupSize;

            ColumnCount = groupsInRow * groupSize;
            RowCount = (_data.Length / _laneDataSize / ColumnCount) + groupCount % groupsInRow;
        }

        public int RowHeader(int row) => row * _groupsInRow;
        public int GroupNum(int row, int column) => _groupsInRow * row + column / _groupSize;

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
        public ReadOnlyCollection<string> Watches { get; }

        public int GroupIndex { get; private set; }
        public int GroupSize { get; private set; }
        public int WaveSize { get; private set; }

        // Data group size may be different from display group size (as entered in visualizer window),
        // because it is always a multiple of wave size (rounded up)
        private int? DataGroupSize
        {
            get
            {
                if (GroupSize > 0 && WaveSize > 0)
                    return ((GroupSize + WaveSize - 1) / WaveSize) * WaveSize;
                return null;
            }
        }

        private readonly BreakStateOutputFile _outputFile;
        private readonly int _laneDataSize; // in dwords

        private readonly uint[] _data;
        private readonly bool _localData;
        private BitArray _fetchedDataWaves; // 1 bit per wavefront data

        public BreakStateData(ReadOnlyCollection<string> watches, BreakStateOutputFile file, byte[] localData = null)
        {
            Watches = watches;
            _outputFile = file;
            _laneDataSize = 1 /* system */ + watches.Count;

            _data = new uint[file.DwordCount];
            _localData = localData != null;
            if (_localData)
            {
                if (file.Offset != 0)
                    throw new ArgumentException("Trim the offset before passing output data to BreakStateData");
                Buffer.BlockCopy(localData, file.Offset, _data, 0, file.DwordCount * 4);
            }
        }

        public int GetGroupCount(int groupSize, int waveSize, int nGroups)
        {
            var realGroupSize = ((groupSize + waveSize - 1) / waveSize) * waveSize; // real group size is always a multiple of wave size
            var realCount = _data.Length / realGroupSize / _laneDataSize;
            // Disabled for now as it should be refactored
            //if (nGroups != 0 && nGroups < realCount)
            //    return nGroups;
            return realCount;
        }

        public WatchView GetSystem()
        {
            if (DataGroupSize is int dataGroupSize)
                return new WatchView(_data, GroupIndex, GroupSize, dataGroupSize, laneDataOffset: 0, _laneDataSize);
            return null;
        }

        public WatchView GetWatch(string watch)
        {
            var watchIndex = Watches.IndexOf(watch);
            if (watchIndex != -1 && DataGroupSize is int dataGroupSize)
                return new WatchView(_data, GroupIndex, GroupSize, dataGroupSize, laneDataOffset: watchIndex + 1 /* system */, _laneDataSize);
            return null;
        }

        public SliceWatchView GetSliceWatch(string watch, int groupsInRow, int nGroups)
        {
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
            return new SliceWatchView(_data, groupsInRow, GroupSize, GetGroupCount(GroupSize, WaveSize, nGroups), laneDataOffset, _laneDataSize);
        }

        public WavemapView GetWavemapView()
        {
            if (GroupSize > 0 && WaveSize > 0)
                return new WavemapView(_data, WaveSize, _laneDataSize, GroupSize, GetGroupCount(GroupSize, WaveSize, 0));
            return null;
        }

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int waveSize, int nGroups, bool fetchWholeFile = false)
        {
            if (waveSize != WaveSize)
            {
                var waveDataSize = waveSize * _laneDataSize;
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

            var waveDataSize = _laneDataSize * WaveSize;
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

            if (_data.Length < nGroups * groupSize * _laneDataSize)
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
