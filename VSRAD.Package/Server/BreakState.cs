using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    public sealed class BreakState
    {
        private static readonly Regex _watchDwordsRegex =
            new Regex(@"max items per instance including system watch: (?<dwords_per_lane>\d+)", RegexOptions.Compiled);
        private static readonly Regex _watchInstancesRegex =
            new Regex(@"\s*(?:instance (?<instance>\d+) (?:breakpoint id: (?<instance_bp>[^\r\n]*)|valid watches: (?<instance_watches>[^\r\n]*)))[\r\n]*", RegexOptions.Compiled);

        public BreakStateData Data { get; }
        public BreakStateDispatchParameters DispatchParameters { get; }
        public BreakTarget Target { get; }
        /// <summary>Mapping of instance ids to indexes into the Target.Breakpoints list. The set of values represents all valid breakpoints (a subset of target breakpoints).</summary>
        public IReadOnlyDictionary<uint, uint> BreakpointIndexPerInstance { get; }
        /// <summary>Indexes into the Target.Breakpoints list of breakpoints that were hit at least once (a subset of valid breakpoints).</summary>
        public SortedSet<uint> HitBreakpoints { get; }

        public BreakState(BreakStateData data, BreakStateDispatchParameters dispatchParameters, BreakTarget target, IReadOnlyDictionary<uint, uint> breakpointIndexPerInstance, uint? checkMagicNumber)
        {
            Data = data;
            DispatchParameters = dispatchParameters;
            Target = target;
            BreakpointIndexPerInstance = breakpointIndexPerInstance;

            HitBreakpoints = new SortedSet<uint>();
            var hitInstances = Data.GetGlobalInstancesHit((int)DispatchParameters.WaveSize, checkMagicNumber);
            foreach (var instanceId in hitInstances)
            {
                if (BreakpointIndexPerInstance.TryGetValue(instanceId, out var breakpointIdx))
                    HitBreakpoints.Add(breakpointIdx);
            }
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

            BreakStateDispatchParameters dispatchParams = null;
            if (dispatchParamsString != null && !BreakStateDispatchParameters.Parse(dispatchParamsString).TryGetResult(out dispatchParams, out var error))
                return error;

            if (dispatchParams != null)
            {
                var dispatchLaneCount = dispatchParams.GridSizeX * dispatchParams.GridSizeY * dispatchParams.GridSizeZ;
                var expectedDwordCount = dispatchLaneCount * dwordsPerLane;
                if (outputFile.DwordCount != expectedDwordCount)
                    return new Error($"Debug data is invalid. Output file does not match the expected size.\r\n\r\n" +
                        $"Grid size as specified in the dispatch parameters file is ({dispatchParams.GridSizeX}, {dispatchParams.GridSizeY}, {dispatchParams.GridSizeZ}), " +
                        $"or {dispatchLaneCount} lanes in total. With {dwordsPerLane} DWORDs per lane, the output file is expected to be {expectedDwordCount} DWORDs long, " +
                        $"but the actual size is {outputFile.DwordCount} DWORDs.");
            }

            var breakData = new BreakStateData(watches, (int)dwordsPerLane, outputFile, localOutputData);
            return new BreakState(breakData, dispatchParams, breakTarget, breakpoints, checkMagicNumber);
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
