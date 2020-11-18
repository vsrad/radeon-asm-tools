using System;
using System.Collections;
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

        public WatchView(uint[] data, int groupOffset, int laneDataOffset, int laneDataSize)
        {
            _data = data;
            _startOffset = groupOffset + laneDataOffset;
            _laneDataSize = laneDataSize;
        }

        // For tests
        public WatchView(uint[] flatWatchData)
        {
            _data = flatWatchData;
            _laneDataSize = 1;
        }

        public uint this[int index]
        {
            get
            {
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
        public ReadOnlyCollection<string> Watches { get; }

        public int GroupIndex { get; private set; }
        public int GroupSize { get; private set; }

        private readonly BreakStateOutputFile _outputFile;
        private readonly int _laneDataSize; // in dwords
        private readonly int _waveDataSize;

        private readonly uint[] _data;
        private const int _wavefrontSize = 64;
        private readonly BitArray _fetchedDataWaves; // 1 bit per wavefront data

        public BreakStateData(ReadOnlyCollection<string> watches, BreakStateOutputFile file, byte[] localData = null)
        {
            Watches = watches;
            _outputFile = file;
            _laneDataSize = 1 /* system */ + watches.Count;
            _waveDataSize = _laneDataSize * _wavefrontSize;

            var outputDwordCount = file.ByteCount / 4;
            var outputWaveCount = outputDwordCount / _waveDataSize;

            _data = new uint[outputDwordCount];

            if (localData != null)
            {
                if (file.ByteCount != localData.Length)
                    throw new ArgumentException($"{nameof(localData)}.Length should be equal to ${nameof(file)}.ByteCount");

                Buffer.BlockCopy(localData, 0, _data, 0, localData.Length);
                _fetchedDataWaves = new BitArray(outputWaveCount, true);
            }
            else
            {
                _fetchedDataWaves = new BitArray(outputWaveCount, false);
            }
        }

        public int GetGroupCount(int groupSize, int nGroups)
        {
            var realCount = _data.Length / groupSize / _laneDataSize;
            // Disabled for now as it should be refactored
            //if (nGroups != 0 && nGroups < realCount)
            //    return nGroups;
            return realCount;
        }

        public WatchView GetSystem()
        {
            var groupOffset = GroupIndex * GroupSize * _laneDataSize;
            return new WatchView(_data, groupOffset, laneDataOffset: 0, _laneDataSize);
        }

        public WatchView GetWatch(string watch)
        {
            var watchIndex = Watches.IndexOf(watch);
            if (watchIndex == -1)
                return null;

            var groupOffset = GroupIndex * GroupSize * _laneDataSize;
            return new WatchView(_data, groupOffset, laneDataOffset: watchIndex + 1 /* system */, _laneDataSize);
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
            return new SliceWatchView(_data, groupsInRow, GroupSize, GetGroupCount(GroupSize, nGroups), laneDataOffset, _laneDataSize);
        }

        public WavemapView GetWavemapView(int waveSize) => new WavemapView(_data, waveSize, Watches.Count + 1, GroupSize, _data.Length / GroupSize / _laneDataSize); // real group count

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int nGroups, bool fetchWholeFile = false)
        {
            var warning = await FetchFilePartAsync(channel, groupIndex, groupSize, nGroups, fetchWholeFile);
            GroupIndex = groupIndex;
            GroupSize = groupSize;
            return warning;
        }

        private async Task<string> FetchFilePartAsync(ICommunicationChannel channel, int groupIndex, int groupSize, int nGroups, bool fetchWholeFile)
        {
            GetRequestedFilePart(groupIndex, groupSize, nGroups, fetchWholeFile, out var waveOffset, out var waveCount);
            if (IsFilePartFetched(waveOffset, waveCount))
                return null;

            var requestedByteOffset = waveOffset * _waveDataSize * 4;
            var requestedByteCount = waveCount * _waveDataSize * 4;

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
            var fetchedWaveCount = response.Data.Length / _waveDataSize / 4;
            MarkFilePartAsFetched(waveOffset, fetchedWaveCount);

            if (response.Timestamp != _outputFile.Timestamp)
                return "Output file has changed since the last debugger execution.";

            if (response.Data.Length < requestedByteCount)
                return $"Group #{groupIndex} is incomplete: expected to read {requestedByteCount} bytes but the output file contains {response.Data.Length}.";

            if (_data.Length < nGroups * groupSize * _laneDataSize)
                return $"Output file has fewer groups than requested (NGroups = {nGroups}, but the file contains only {GetGroupCount(groupSize, nGroups)})";

            return null;
        }

        private void GetRequestedFilePart(int groupIndex, int groupSize, int nGroups, bool fetchWholeFile, out int waveOffset, out int waveCount)
        {
            if (fetchWholeFile)
            {
                groupSize += groupSize % _wavefrontSize; // round up to a multiple of _wavefrontSize
                waveCount = nGroups * groupSize / _wavefrontSize;
                if (waveCount == 0 || waveCount > _fetchedDataWaves.Length)
                    waveCount = _fetchedDataWaves.Length;
                waveOffset = 0;
            }
            else // single group
            {
                var groupStart = groupIndex * groupSize;
                var groupEnd = (groupIndex + 1) * groupSize;
                var startWaveIndex = groupStart / _wavefrontSize;
                var endWaveIndex = groupEnd / _wavefrontSize + (groupEnd % _wavefrontSize != 0 ? 1 : 0); // ceil

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
