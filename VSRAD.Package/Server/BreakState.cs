using System;
using System.Linq;
using System.Text.RegularExpressions;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public sealed class BreakState
    {
        private static readonly Regex _watchesRegex = new Regex(@"\s*(?:Instance (?<instances>\d+):(?<instance_watches>[^\r\n]*)[\r\n]*)*", RegexOptions.Compiled);

        public BreakStateData Data { get; }
        public BreakStateDispatchParameters DispatchParameters { get; }
        public DateTime ExecutedAt { get; } = DateTime.Now;

        public BreakState(BreakStateData breakStateData, BreakStateDispatchParameters dispatchParameters)
        {
            Data = breakStateData;
            DispatchParameters = dispatchParameters;
        }

        public static Result<BreakState> CreateBreakState(string validWatchesString, string dispatchParamsString, BreakStateOutputFile outputFile, byte[] localOutputData)
        {
            var watchesMatch = _watchesRegex.Match(validWatchesString);
            if (!watchesMatch.Success)
                return new Error($@"Could not read the valid watches file.

The following is an example of the expected file contents:

Instance 0:a;b
Instance 1:
Instance 2:b;c;d

While the actual contents are:

{validWatchesString}");

            var instances = watchesMatch.Groups["instances"].Captures.Cast<Capture>().Select(c => uint.Parse(c.Value));
            var watches = watchesMatch.Groups["instance_watches"].Captures.Cast<Capture>().Select(c => c.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            var instanceWatches = instances.Zip(watches, (i, w) => (i, w)).ToDictionary(p => p.i, p => p.w);

            BreakStateDispatchParameters dispatchParams = null;
            if (dispatchParamsString != null && !BreakStateDispatchParameters.Parse(dispatchParamsString).TryGetResult(out dispatchParams, out var error))
                return error;

            if (dispatchParams != null)
            {
                var dispatchLaneCount = dispatchParams.GridSizeX * dispatchParams.GridSizeY * dispatchParams.GridSizeZ;
                var dwordsPerLane = BreakStateData.GetDwordsPerLane(instanceWatches);
                var expectedDwordCount = (int)dispatchLaneCount * dwordsPerLane;
                if (outputFile.DwordCount != expectedDwordCount)
                    return new Error($"Output file does not match the expected size.\r\n\r\n" +
                        $"Grid size as specified in the dispatch parameters file is ({dispatchParams.GridSizeX}, {dispatchParams.GridSizeY}, {dispatchParams.GridSizeZ}), " +
                        $"or {dispatchLaneCount} lanes in total. With {dwordsPerLane} DWORDs per lane, the output file is expected to be {expectedDwordCount} DWORDs long, " +
                        $"but the actual size is {outputFile.DwordCount} DWORDs.");
            }

            var breakData = new BreakStateData(instanceWatches, outputFile, localOutputData);
            return new BreakState(breakData, dispatchParams);
        }
    }
}
