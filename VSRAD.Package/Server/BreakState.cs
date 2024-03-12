using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public sealed class WatchMeta
    {
        public List<(uint Instance, uint? DataSlot, uint? ListSize)> Instances { get; } = new List<(uint Instance, uint? DataSlot, uint? ListSize)>();
        public List<WatchMeta> ListItems { get; } = new List<WatchMeta>();

        public WatchMeta() { }

        public WatchMeta(IEnumerable<(uint Instance, uint? DataSlot, uint? ListSize)> instances, IEnumerable<WatchMeta> listItems)
        {
            Instances.AddRange(instances);
            ListItems.AddRange(listItems);
        }
    }

    public sealed class WaveStatus
    {
        public uint? InstanceId { get; }
        public uint? BreakpointIndex { get; }
        public bool Scc { get; }
        public ulong Exec { get; }
        public bool PartialExec => Exec != 0xffffffff_ffffffff;

        public WaveStatus(uint? instanceId, uint? breakpointIndex, bool scc, ulong exec)
        {
            InstanceId = instanceId;
            BreakpointIndex = breakpointIndex;
            Scc = scc;
            Exec = exec;
        }
    }

    public sealed class BreakState
    {
        private static readonly Regex _watchDwordsRegex =
            new Regex(@"max items per instance including system watch: (?<dwords_per_lane>\d+)", RegexOptions.Compiled);
        private static readonly Regex _watchInstancesRegex =
            new Regex(@"\s*(?:instance (?<instance>\d+) (?:breakpoint id: (?<instance_bp>[^\r\n]*)|valid watches: (?<instance_watches>[^\r\n]*)))[\r\n]*", RegexOptions.Compiled);
        private static readonly Regex _watchIndexRegex =
            new Regex(@"\[(\d+)\]$", RegexOptions.Compiled);

        public BreakTarget Target { get; }
        public IReadOnlyDictionary<string, WatchMeta> Watches { get; }
        public BreakStateDispatchParameters Dispatch { get; }
        /// <summary>Mapping of instance ids to indexes into the Target.Breakpoints list. The set of values represents all valid breakpoints (a subset of target breakpoints).</summary>
        public IReadOnlyDictionary<uint, uint> BreakpointIndexPerInstance { get; }
        /// <summary>Indexes into the Target.Breakpoints list of breakpoints that were hit at least once (a subset of valid breakpoints).</summary>
        public SortedSet<uint> HitBreakpoints { get; }
        /// <summary>Per-wave status information extracted from the System watch (WavesPerGroup * NumGroups elements). InstanceId and BreakpointIndex are null if the wave has not hit a breakpoint (magic number mismatch).</summary>
        public IReadOnlyList<WaveStatus> WaveStatus { get; }

        public uint DwordsPerLane { get; }
        public uint TotalNumLanes { get; }
        public uint WavesPerGroup { get; }
        public uint GroupSize { get; } // multiple of wave size
        public uint NumGroups { get; }
        public uint GroupIndex { get; private set; } = 0;

        // struct lane_data {
        // uint32_t system;
        // uint32_t watches[n_watches];
        // };
        // lane_data log_file[group_count][group_size]
        //
        // system[wave_size] is spread across a wave (system[0] in lane 0, system[1] in lane 1, ...)
        private readonly uint[] _data;
        private readonly BitArray _fetchedDataWaves; // 1 bit per wavefront data, null if _data is loaded from a local file
        private readonly BreakStateOutputFile _outputFile;

        public BreakState(BreakTarget target, IReadOnlyDictionary<string, WatchMeta> watches, BreakStateDispatchParameters dispatchParameters, IReadOnlyDictionary<uint, uint> breakpointIndexPerInstance, uint dwordsPerLane, BreakStateOutputFile file, uint? checkMagicNumber, byte[] localData = null)
        {
            Target = target;
            Watches = watches;
            Dispatch = dispatchParameters;
            BreakpointIndexPerInstance = breakpointIndexPerInstance;

            DwordsPerLane = dwordsPerLane;
            TotalNumLanes = (uint)file.DwordCount / dwordsPerLane;
            WavesPerGroup = MathUtils.RoundUpQuotient(Dispatch.GroupSizeX, Dispatch.WaveSize);
            GroupSize = WavesPerGroup * Dispatch.WaveSize;
            NumGroups = TotalNumLanes / GroupSize;
            _outputFile = file;

            _data = new uint[file.DwordCount];
            if (localData != null)
            {
                if (file.Offset != 0)
                    throw new ArgumentException("Trim the offset before passing output data to BreakStateData");
                Buffer.BlockCopy(localData, file.Offset, _data, 0, file.DwordCount * 4);
            }
            else
            {
                _fetchedDataWaves = new BitArray((int)MathUtils.RoundUpQuotient(TotalNumLanes, Dispatch.WaveSize), false);
            }

            WaveStatus = ReadWaveStatus(checkMagicNumber);
            HitBreakpoints = new SortedSet<uint>(WaveStatus.Where(s => s.BreakpointIndex != null).Select(s => (uint)s.BreakpointIndex));
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

        public WaveStatus GetWaveStatus(uint waveIndex)
        {
            if (waveIndex >= WavesPerGroup)
                throw new ArgumentOutOfRangeException(nameof(waveIndex), waveIndex, $"Wave index must be less than the number of waves per group ({WavesPerGroup})");
            var globalWaveIndex = WavesPerGroup * GroupIndex + waveIndex;
            return WaveStatus[(int)globalWaveIndex];
        }

        /// <returns>An array of exactly n=WaveSize elements, with 0s for missing data</returns>
        public uint[] GetWatchData(uint waveIndex, uint dataSlot)
        {
            if (waveIndex >= WavesPerGroup)
                throw new ArgumentOutOfRangeException(nameof(waveIndex), waveIndex, $"Wave index must be less than the number of waves per group ({WavesPerGroup})");
            if (dataSlot >= DwordsPerLane)
                throw new ArgumentOutOfRangeException(nameof(dataSlot), dataSlot, $"Data slot must be less than the number of dwords per lane ({DwordsPerLane})");

            var startThreadId = (WavesPerGroup * GroupIndex + waveIndex) * Dispatch.WaveSize;
            var dataStart = startThreadId * DwordsPerLane + dataSlot;
            var dataEnd = Math.Min((startThreadId + Dispatch.WaveSize) * DwordsPerLane, _data.Length);

            var watchData = new uint[Dispatch.WaveSize];
            for (uint i = 0, offset = dataStart; offset < dataEnd; i += 1, offset += DwordsPerLane)
                watchData[i] = _data[offset];
            return watchData;
        }

        public const int SystemMagicNumberLane = 0;
        public const int SystemInstanceIdLane = 2;
        public const int SystemSccLane = 3;
        public const int SystemExecLoLane = 8;
        public const int SystemExecHiLane = 9;

        public uint[] GetSystemData(uint waveIndex) => GetWatchData(waveIndex, 0);

        private IReadOnlyList<WaveStatus> ReadWaveStatus(uint? checkMagicNumber)
        {
            var waveStatus = new List<WaveStatus>();
            var (magicNumberOffset, instanceIdOffset) = (SystemMagicNumberLane * DwordsPerLane, SystemInstanceIdLane * DwordsPerLane);
            var (sccOffset, execLoOffset, execHiOffset) = (SystemSccLane * DwordsPerLane, SystemExecLoLane * DwordsPerLane, SystemExecHiLane * DwordsPerLane);
            var lastOffset = Enumerable.Max(new[] { magicNumberOffset, instanceIdOffset, sccOffset, execLoOffset, execHiOffset });
            var lastSystemLane = Enumerable.Max(new[] { SystemMagicNumberLane, SystemInstanceIdLane, SystemSccLane, SystemExecLoLane, SystemExecHiLane });
            if (lastSystemLane >= Dispatch.WaveSize)
                throw new ArgumentException($"Cannot read wave status from debug data: expected at least {lastSystemLane + 1} lanes inside the System watch, but wave size is {Dispatch.WaveSize}");
            var stride = Dispatch.WaveSize * DwordsPerLane;
            for (uint o = 0; o + lastOffset < _data.Length; o += stride)
            {
                var (sysMagicNumber, sysInstanceId) = (_data[o + magicNumberOffset], _data[o + instanceIdOffset]);
                var (sysScc, sysExec) = (_data[o + sccOffset], (((ulong)_data[o + execHiOffset]) << 32) | _data[o + execLoOffset]);

                bool hitBreak = !(checkMagicNumber is uint expectedMagicNumber && expectedMagicNumber != sysMagicNumber);
                var instanceId = hitBreak ? sysInstanceId : (uint?)null;
                var breakpointIndex = hitBreak && BreakpointIndexPerInstance.TryGetValue(sysInstanceId, out var i) ? i : (uint?)null;

                waveStatus.Add(new WaveStatus(instanceId: instanceId, breakpointIndex: breakpointIndex, scc: sysScc != 0, exec: sysExec));
            }
            return waveStatus;
        }

        public async Task<string> ChangeGroupWithWarningsAsync(ICommunicationChannel channel, uint groupIndex, bool fetchWholeFile = false, CancellationToken cancellationToken = default)
        {
            string warning = null;
            if (_fetchedDataWaves != null)
                warning = await FetchFilePartAsync(channel, groupIndex, fetchWholeFile, cancellationToken);
            GroupIndex = groupIndex;
            return warning;
        }

        private async Task<string> FetchFilePartAsync(ICommunicationChannel channel, uint groupIndex, bool fetchWholeFile, CancellationToken cancellationToken)
        {
            GetRequestedFilePart(groupIndex, fetchWholeFile, out var waveOffset, out var waveCount);
            if (IsFilePartFetched(waveOffset, waveCount))
                return null;

            var waveDataSize = DwordsPerLane * Dispatch.WaveSize;
            var requestedByteOffset = (int)(waveOffset * waveDataSize * 4);
            var requestedByteCount = (int)Math.Min(waveCount * waveDataSize * 4, _outputFile.DwordCount * 4);

            var response = await channel.SendWithReplyAsync<DebugServer.IPC.Responses.ResultRangeFetched>(
                new DebugServer.IPC.Commands.FetchResultRange
                {
                    FilePath =  _outputFile.Path,
                    BinaryOutput = _outputFile.BinaryOutput,
                    ByteOffset = requestedByteOffset,
                    ByteCount = requestedByteCount,
                    OutputOffset = _outputFile.Offset
                }, cancellationToken).ConfigureAwait(false);

            if (response.Status != DebugServer.IPC.Responses.FetchStatus.Successful)
                return "Output file could not be opened.";

            Buffer.BlockCopy(response.Data, 0, _data, requestedByteOffset, response.Data.Length);
            var fetchedWaveCount = (uint)response.Data.Length / waveDataSize / 4;
            MarkFilePartAsFetched(waveOffset, fetchedWaveCount);

            if (response.Timestamp != _outputFile.Timestamp)
                return "Output file has changed since the last debugger execution.";

            if (response.Data.Length < requestedByteCount)
                return $"Group #{groupIndex} is incomplete: expected to read {requestedByteCount} bytes but the output file contains {response.Data.Length}.";

            return null;
        }

        private void GetRequestedFilePart(uint groupIndex, bool fetchWholeFile, out uint waveOffset, out uint waveCount)
        {
            if (fetchWholeFile)
            {
                waveCount = MathUtils.RoundUpQuotient(TotalNumLanes, Dispatch.WaveSize);
                if (waveCount == 0 || waveCount > _fetchedDataWaves.Length)
                    waveCount = (uint)_fetchedDataWaves.Length;
                waveOffset = 0;
            }
            else // single group
            {
                var groupStart = groupIndex * GroupSize;
                var groupEnd = (groupIndex + 1) * GroupSize;
                var startWaveIndex = groupStart / Dispatch.WaveSize;
                var endWaveIndex = MathUtils.RoundUpQuotient(groupEnd, Dispatch.WaveSize);

                waveCount = endWaveIndex - startWaveIndex;
                waveOffset = startWaveIndex;
            }
        }

        private bool IsFilePartFetched(uint waveOffset, uint waveCount)
        {
            for (uint i = waveOffset; i < waveOffset + waveCount; ++i)
                if (!_fetchedDataWaves[(int)i])
                    return false;
            return true;
        }

        private void MarkFilePartAsFetched(uint waveOffset, uint waveCount)
        {
            for (uint i = waveOffset; i < waveOffset + waveCount; ++i)
                _fetchedDataWaves[(int)i] = true;
        }

        public static Result<BreakState> CreateBreakState(BreakTarget breakTarget, IReadOnlyList<string> watchNames, string validWatchesString, string dispatchParamsString, BreakStateOutputFile outputFile, byte[] localOutputData, uint? checkMagicNumber)
        {
            var dwordsMatch = _watchDwordsRegex.Match(validWatchesString);
            var (breakpoints, watches) = (new Dictionary<uint, uint>(), new Dictionary<string, WatchMeta>());
            if (!dwordsMatch.Success || !TryParseInstances(breakpoints, watches, breakTarget, watchNames, validWatchesString))
                return new Error($@"Could not read the valid watches file.

The following is an example of the expected file contents:

max items per instance including system watch: 10
instance 0 breakpoint id: 0
Instance 0 valid watches: [1,[1,0,1,1],[1,[1,1],0,[1],[],1]]

Where ""breakpoint id"" refers to an item from the target breakpoints file and ""valid watches"" is a list referring to items from the target watches file.

The actual file contents are:

{validWatchesString}");

            var dwordsPerLane = uint.Parse(dwordsMatch.Groups["dwords_per_lane"].Value);

            if (!BreakStateDispatchParameters.Parse(dispatchParamsString).TryGetResult(out var dispatchParams, out var error))
                return error;
            var dispatchLaneCount = dispatchParams.GridSizeX * dispatchParams.GridSizeY * dispatchParams.GridSizeZ;
            var expectedDwordCount = dispatchLaneCount * dwordsPerLane;
            if (outputFile.DwordCount != expectedDwordCount)
                return new Error($"Debug data is invalid. Output file does not match the expected size.\r\n\r\n" +
                    $"Grid size as specified in the dispatch parameters file is ({dispatchParams.GridSizeX}, {dispatchParams.GridSizeY}, {dispatchParams.GridSizeZ}), " +
                    $"or {dispatchLaneCount} lanes in total. With {dwordsPerLane} DWORDs per lane, the output file is expected to be {expectedDwordCount} DWORDs long, " +
                    $"but the actual size is {outputFile.DwordCount} DWORDs.");

            return new BreakState(breakTarget, watches, dispatchParams, breakpoints, dwordsPerLane, outputFile, checkMagicNumber, localOutputData);
        }

        private static bool TryParseInstances(Dictionary<uint, uint> breakpoints, Dictionary<string, WatchMeta> watches, BreakTarget breakTarget, IReadOnlyList<string> watchNames, string validWatchesString)
        {
            foreach (var instance in _watchInstancesRegex.Matches(validWatchesString).Cast<Match>().GroupBy(m => uint.Parse(m.Groups["instance"].Value)))
            {
                if (!(instance.FirstOrDefault(m => m.Groups["instance_bp"].Success) is Match mBreakpoint))
                    return false;
                if (!uint.TryParse(mBreakpoint.Groups["instance_bp"].Value, out var breakpointIdx))
                    return false;

                if (breakpointIdx < breakTarget.Breakpoints.Count)
                    breakpoints[instance.Key] = breakpointIdx;

                if (!(instance.FirstOrDefault(m => m.Groups["instance_watches"].Success) is Match mWatches))
                    return false;
                if (!TryParseInstanceWatches(watches, instance.Key, watchNames, mWatches.Groups["instance_watches"].Value))
                    return false;
            }
            return true;
        }

        private static bool TryParseInstanceWatches(Dictionary<string, WatchMeta> watches, uint instance, IReadOnlyList<string> watchNames, string validWatches)
        {
            WatchMeta GetListItemMeta(WatchMeta acc, uint idx)
            {
                while (idx >= acc.ListItems.Count)
                    acc.ListItems.Add(new WatchMeta());
                return acc.ListItems[(int)idx];
            }
            var watchIndexes = new List<uint>();
            uint dataSlot = 1; // slot 0 is reserved for System watch
            foreach (char c in validWatches)
            {
                switch (c)
                {
                    case '[':
                        watchIndexes.Add(0);
                        break;
                    case '0':
                    case '1':
                        if (watchIndexes.Count == 0 || watchIndexes[0] >= watchNames.Count)
                            return false;
                        if (!watches.TryGetValue(watchNames[(int)watchIndexes[0]], out var rootWatchMeta))
                            watches.Add(watchNames[(int)watchIndexes[0]], rootWatchMeta = new WatchMeta());
                        var itemMeta = watchIndexes.Skip(1).Aggregate(rootWatchMeta, GetListItemMeta);
                        if (c == '1')
                            itemMeta.Instances.Add((instance, DataSlot: dataSlot++, ListSize: null));
                        watchIndexes[watchIndexes.Count - 1]++;
                        break;
                    case ']':
                        if (watchIndexes.Count == 0)
                            return false;
                        var length = watchIndexes[watchIndexes.Count - 1];
                        watchIndexes.RemoveAt(watchIndexes.Count - 1);
                        if (watchIndexes.Count > 0)
                        {
                            if (watchIndexes[0] >= watchNames.Count)
                                return false;
                            if (!watches.TryGetValue(watchNames[(int)watchIndexes[0]], out rootWatchMeta))
                                watches.Add(watchNames[(int)watchIndexes[0]], rootWatchMeta = new WatchMeta());
                            var parentListMeta = watchIndexes.Skip(1).Aggregate(rootWatchMeta, GetListItemMeta);
                            parentListMeta.Instances.Add((instance, DataSlot: null, ListSize: length));
                            watchIndexes[watchIndexes.Count - 1]++;
                        }
                        break;
                }
            }
            return watchIndexes.Count == 0;
        }
    }
}
