using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        public ReadOnlyCollection<string> Watches { get; }

        private uint _groupSize = 512;
        public uint GroupSize
        {
            get => _groupSize;
            set
            {
                if (value != _groupSize)
                {
                    _groupSize = value;
                    _valuesByGroup.Clear();
                }
            }
        }

        // struct record {
        // uint32_t hidden;
        // uint32_t watches[n_watches];
        // };
        // record log_file[group_count][group_size]
        //
        // hidden is an array spread across 64 records

        private uint _groupIndex;

        private readonly Dictionary<uint, uint[]> _valuesByGroup = new Dictionary<uint, uint[]>();

        private readonly Options.OutputFile _outputFile;
        private readonly DateTime _outputTimestamp;
        private readonly uint _outputDwordCount;
        private readonly uint _recordSize;
        private readonly ICommunicationChannel _channel;
        private readonly int _outputOffset;

        public BreakState(Options.OutputFile outputFile, DateTime outputTimestamp, uint outputByteCount, int outputOffset, ReadOnlyCollection<string> watches, ICommunicationChannel channel)
        {
            _outputFile = outputFile;
            _outputTimestamp = outputTimestamp;
            _outputDwordCount = outputByteCount / 4;
            _recordSize = 1 /* hidden */ + (uint)watches.Count;
            Watches = watches;
            _channel = channel;
            _outputOffset = outputOffset;
        }

        public uint[] System { get; } = new uint[512];

        public uint GetGroupCount(uint groupSize) => _outputDwordCount / groupSize / _recordSize;

        public bool TryGetWatch(string watch, out uint[] values)
        {
            var watchIndex = Watches.IndexOf(watch);

            if (watchIndex == -1)
            {
                values = null;
                return false;
            }

            values = new uint[GroupSize];

            for (uint valueIdx = 0, dwordIdx = (uint)watchIndex + 1 /* system */;
                 dwordIdx < _valuesByGroup[_groupIndex].Length && valueIdx < GroupSize;
                 valueIdx++, dwordIdx += _recordSize)
            {
                values[valueIdx] = _valuesByGroup[_groupIndex][dwordIdx];
            }

            return true;
        }

        public async Task<bool> ChangeGroupAsync(uint groupIndex, uint groupSize)
        {
            GroupSize = groupSize;
            if (_valuesByGroup.TryGetValue(groupIndex, out var values))
            {
                _groupIndex = groupIndex;
                SetSystemFromGroup(values);
                return true;
            }

            values = await FetchGroupAsync(groupIndex).ConfigureAwait(false);
            if (values == null) return false;

            SetSystemFromGroup(values);
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            _valuesByGroup[groupIndex] = values;
            _groupIndex = groupIndex;
            return true;
        }

        private void SetSystemFromGroup(uint[] groupValues)
        {
            for (uint sysIdx = 0, dwordIdx = 0; dwordIdx < groupValues.Length; sysIdx++, dwordIdx += _recordSize)
            {
                System[sysIdx] = groupValues[dwordIdx];
            }
        }

        private async Task<uint[]> FetchGroupAsync(uint groupIndex)
        {
            int byteOffset = (int)((4 * groupIndex * GroupSize * _recordSize) + _outputOffset);
            int byteCount = (int)(4 * GroupSize * _recordSize);
            var response = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.ResultRangeFetched>(
                new DebugServer.IPC.Commands.FetchResultRange
                {
                    FilePath = _outputFile.Path,
                    BinaryOutput = _outputFile.BinaryOutput,
                    ByteOffset = byteOffset,
                    ByteCount = byteCount
                }).ConfigureAwait(false);

            if (response.Status != DebugServer.IPC.Responses.FetchStatus.Successful)
            {
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                Errors.ShowWarning("Output file could not be opened.");
                return null;
            }
            if (response.Timestamp != _outputTimestamp)
            {
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                Errors.ShowWarning("Output file has changed since the last debugger execution.");
                return null;
            }
            if (response.Data.Length < byteCount)
            {
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                Errors.ShowWarning($"Group #{groupIndex} is incomplete: expected to read {byteCount} bytes but the output file contains {response.Data.Length}.");
                return null;
            }
            uint[] data = new uint[response.Data.Length / 4];
            Buffer.BlockCopy(response.Data, 0, data, 0, response.Data.Length);
            return data;
        }
    }
}
