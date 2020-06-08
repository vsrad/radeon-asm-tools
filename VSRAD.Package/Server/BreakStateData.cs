using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VSRAD.Package.Options;

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

    public sealed class SliceWatchWiew
    {
        public int ColumnCount { get; }
        public int RowCount { get; }

        private readonly int _laneDataOffset;
        private readonly int _laneDataSize;

        private readonly uint[] _data;

        private readonly int _minValue;
        private readonly int _maxValue;

        public SliceWatchWiew(uint[] data, int groupsInRow, int groupSize, int laneDataOffset, int laneDataSize)
        {
            _data = data;
            _laneDataOffset = laneDataOffset;
            _laneDataSize = laneDataSize;

            ColumnCount = groupsInRow * groupSize;
            RowCount = _data.Length / _laneDataSize / ColumnCount;

            // TODO: pass VariableType and cast appropriately (union via StructLayout?)
            _minValue = int.MaxValue;
            _maxValue = int.MinValue;
            for (int row = 0; row < RowCount; ++row)
            {
                for (int col = 0; col < ColumnCount; ++col)
                {
                    if ((int)this[row, col] < _minValue)
                        _minValue = (int)this[row, col];
                    if ((int)this[row, col] > _maxValue)
                        _maxValue = (int)this[row, col];
                }
            }
        }

        // For tests
        public SliceWatchWiew(uint[] flatWatchData)
        {
            _data = flatWatchData;
        }

        public uint this[int row, int column]
        {
            get
            {
                var groupIdx = row * ColumnCount + column;
                var dwordIdx = groupIdx * _laneDataSize + _laneDataOffset;
                return _data[dwordIdx];
            }
        }

        public float GetRelativeValue(int row, int column) =>
            ((float)((int)this[row, column] - _minValue)) / (_maxValue - _minValue);
    }

    public sealed class BreakStateData
    {
        public ReadOnlyCollection<string> Watches { get; }

        public int GroupIndex { get; private set; }
        public int GroupSize { get; private set; }

        private readonly OutputFile _outputFile;
        private readonly DateTime _outputFileTimestamp;
        private readonly int _outputOffset;
        private readonly int _laneDataSize; // in dwords
        private readonly int _waveDataSize;

        private readonly uint[] _data;
        private const int _wavefrontSize = 64;
        private readonly BitArray _fetchedDataWaves; // 1 bit per wavefront data

        public BreakStateData(ReadOnlyCollection<string> watches, OutputFile file, DateTime fileTimestamp, int outputByteCount, int outputOffset)
        {
            Watches = watches;
            _outputFile = file;
            _outputFileTimestamp = fileTimestamp;
            _outputOffset = outputOffset;
            _laneDataSize = 1 /* system */ + watches.Count;
            _waveDataSize = _laneDataSize * _wavefrontSize;

            var outputDwordCount = outputByteCount / 4;
            var outputWaveCount = outputDwordCount / _waveDataSize;
            _data = new uint[outputDwordCount];
            _fetchedDataWaves = new BitArray(outputWaveCount);
        }

        public uint GetGroupCount(uint groupSize) => (uint)(_data.Length / groupSize / _laneDataSize);

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

        public SliceWatchWiew GetSliceWatch(string watch, int groupsInRow)
        {
            var watchIndex = Watches.IndexOf(watch);
            if (watchIndex == -1)
                return null;

            return new SliceWatchWiew(_data, groupsInRow, GroupSize, laneDataOffset: watchIndex + 1 /* system */, _laneDataSize);
        }

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, int groupIndex, int groupSize)
        {
            if (groupSize % _wavefrontSize != 0)
                throw new NotImplementedException($"BreakStateData assumes that group sizes are a multiple of {_wavefrontSize} (wavefront size)");

            int byteOffset = 4 * groupIndex * groupSize * _laneDataSize;
            int byteCount = 4 * groupSize * _laneDataSize;

            if (IsGroupFetched(byteOffset, byteCount))
            {
                GroupIndex = groupIndex;
                GroupSize = groupSize;
                return null;
            }

            var response = await channel.SendWithReplyAsync<DebugServer.IPC.Responses.ResultRangeFetched>(
                new DebugServer.IPC.Commands.FetchResultRange
                {
                    FilePath = _outputFile.Path,
                    BinaryOutput = _outputFile.BinaryOutput,
                    ByteOffset = byteOffset,
                    ByteCount = byteCount,
                    OutputOffset = _outputOffset
                }).ConfigureAwait(false);

            SetGroupData(byteOffset, byteCount, response.Data);
            GroupIndex = groupIndex;
            GroupSize = groupSize;

            if (response.Status != DebugServer.IPC.Responses.FetchStatus.Successful)
                return "Output file could not be opened.";

            if (response.Timestamp != _outputFileTimestamp)
                return "Output file has changed since the last debugger execution.";

            if (response.Data.Length < byteCount)
                return $"Group #{groupIndex} is incomplete: expected to read {byteCount} bytes but the output file contains {response.Data.Length}.";

            return null;
        }

        private bool IsGroupFetched(int byteOffset, int byteCount)
        {
            int byteWaveSize = 4 * _waveDataSize;
            for (int i = byteOffset / byteWaveSize; i < (byteOffset + byteCount) / byteWaveSize; ++i)
                if (!_fetchedDataWaves[i])
                    return false;
            return true;
        }

        private void SetGroupData(int byteOffset, int byteCount, byte[] groupData)
        {
            Buffer.BlockCopy(groupData, 0, _data, byteOffset, groupData.Length);

            int byteWaveSize = 4 * _waveDataSize;
            for (int i = byteOffset / byteWaveSize; i < (byteOffset + byteCount) / byteWaveSize; ++i)
                _fetchedDataWaves[i] = true;
        }
    }
}
