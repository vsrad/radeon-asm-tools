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
            new Regex(@"\s*(?:instance (?<instance>\d+) (?:breakpoint: (?<instance_bp>[^\r\n]*)|watchnames: (?<instance_names>[^\r\n]*)|watchitems: (?<instance_items>[^\r\n]*)))[\r\n]*", RegexOptions.Compiled);

        public BreakStateData Data { get; }
        public BreakStateDispatchParameters DispatchParameters { get; }
        public BreakTarget Target { get; }
        /// <summary>Mapping of instance ids to indexes into the Target.Breakpoints list. Breakpoint indexes are guaranteed to be valid (within the breakpoint list bounds).</summary>
        public IReadOnlyDictionary<uint, uint> BreakpointIndexPerInstance { get; }
        public DateTime ExecutedAt { get; } = DateTime.Now;

        public BreakState(BreakStateData data, BreakStateDispatchParameters dispatchParameters, BreakTarget target, IReadOnlyDictionary<uint, uint> breakpointIndexPerInstance)
        {
            Data = data;
            DispatchParameters = dispatchParameters;
            Target = target;
            BreakpointIndexPerInstance = breakpointIndexPerInstance;
        }

        public static Result<BreakState> CreateBreakState(BreakTarget breakTarget, string validWatchesString, string dispatchParamsString, BreakStateOutputFile outputFile, byte[] localOutputData)
        {
            var dwordsMatch = _watchDwordsRegex.Match(validWatchesString);
            var (watches, breakpoints) = (new Dictionary<string, WatchMeta>(), new Dictionary<uint, uint>());
            if (!dwordsMatch.Success || !TryParseInstances(breakTarget, breakpoints, watches, validWatchesString))
                return new Error($@"Could not read the valid watches file.

The following is an example of the expected file contents:

max items per instance including system watch: 10
instance 0 breakpoint: 0
Instance 0 watchnames: a;b;c
Instance 0 watchitems: [1,[1,0,1,1],[1,[1,1],0,[1],[],1]]

While the actual contents are:

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
            return new BreakState(breakData, dispatchParams, breakTarget, breakpoints);
        }

        private static bool TryParseInstances(BreakTarget breakTarget, Dictionary<uint, uint> breakpoints, Dictionary<string, WatchMeta> watches, string validWatchesString)
        {
            foreach (var instance in _watchInstancesRegex.Matches(validWatchesString).Cast<Match>().GroupBy(m => uint.Parse(m.Groups["instance"].Value)))
            {
                if (!(instance.FirstOrDefault(m => m.Groups["instance_bp"].Success) is Match mBreakpoint))
                    return false;
                if (!uint.TryParse(mBreakpoint.Groups["instance_bp"].Value, out var breakpointIdx))
                    return false;

                if (breakpointIdx < breakTarget.Breakpoints.Count)
                    breakpoints[instance.Key] = breakpointIdx;

                if (!(instance.FirstOrDefault(m => m.Groups["instance_names"].Success) is Match mNames && instance.FirstOrDefault(m => m.Groups["instance_items"].Success) is Match mItems))
                    return false;
                if (!TryParseInstanceWatches(watches, instance.Key, mNames.Groups["instance_names"].Value, mItems.Groups["instance_items"].Value))
                    return false;
            }
            return true;
        }

        private static bool TryParseInstanceWatches(Dictionary<string, WatchMeta> watches, uint instance, string instanceNames, string instanceItems)
        {
            var watchNames = instanceNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            WatchMeta GetListItemMeta(WatchMeta acc, uint idx)
            {
                while (idx >= acc.ListItems.Count)
                    acc.ListItems.Add(new WatchMeta());
                return acc.ListItems[(int)idx];
            }
            var watchIndexes = new List<uint>();
            uint dataSlot = 1; // slot 0 is reserved for System watch
            foreach (char c in instanceItems)
            {
                switch (c)
                {
                    case '[':
                        watchIndexes.Add(0);
                        break;
                    case '0':
                    case '1':
                        if (watchIndexes.Count == 0 || watchIndexes[0] >= watchNames.Length)
                            return false;
                        if (!watches.TryGetValue(watchNames[watchIndexes[0]], out var rootWatchMeta))
                            watches.Add(watchNames[watchIndexes[0]], rootWatchMeta = new WatchMeta());
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
                            if (watchIndexes[0] >= watchNames.Length)
                                return false;
                            if (!watches.TryGetValue(watchNames[watchIndexes[0]], out rootWatchMeta))
                                watches.Add(watchNames[watchIndexes[0]], rootWatchMeta = new WatchMeta());
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
