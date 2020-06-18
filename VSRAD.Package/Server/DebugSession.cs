using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    internal sealed class DebugSession
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly IFileSynchronizationManager _fileSynchronizationManager;

        public DebugSession(
            IProject project,
            ICommunicationChannel channel,
            IFileSynchronizationManager fileSynchronizationManager,
            IOutputWindowManager outputWindowManager,
            IErrorListManager errorListManager)
        {
            _project = project;
            _channel = channel;
            _fileSynchronizationManager = fileSynchronizationManager;
        }

        public async Task<DebugRunResult> ExecuteAsync(uint[] breakLines, ReadOnlyCollection<string> watches)
        {
            var execTimer = Stopwatch.StartNew();
            var evaluator = await _project.GetMacroEvaluatorAsync(breakLines).ConfigureAwait(false);
            var options = await _project.Options.Profile.Debugger.EvaluateAsync(evaluator).ConfigureAwait(false);

            await _fileSynchronizationManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var runner = new ActionRunner(_channel);
            var result = await runner.RunAsync(options.Steps, new[] { options.OutputFile, options.WatchesFile, options.StatusFile }).ConfigureAwait(false);

            if (!result.Successful)
                return new DebugRunResult(result, null, null);

            var fetchCommands = new List<ICommand>();
            if (!string.IsNullOrEmpty(options.WatchesFile.Path))
                fetchCommands.Add(new FetchResultRange { FilePath = new[] { options.WatchesFile.Path } });
            if (!string.IsNullOrEmpty(options.StatusFile.Path))
                fetchCommands.Add(new FetchResultRange { FilePath = new[] { options.StatusFile.Path } });
            fetchCommands.Add(new FetchMetadata { FilePath = new[] { options.OutputFile.Path } });

            var fetchReplies = await _channel.SendBundleAsync(fetchCommands);
            var fetchIndex = 0;
            if (!string.IsNullOrEmpty(options.WatchesFile.Path))
            {
                var watchesResponse = (ResultRangeFetched)fetchReplies[fetchIndex++];
                var parseResult = ReadValidWatches(watchesResponse, options.WatchesFile, runner.GetInitialFileTimestamp(options.WatchesFile.Path));
                if (!parseResult.TryGetResult(out watches, out var parseError))
                    return new DebugRunResult(result, parseError, null);
            }
            var statusString = "";
            if (!string.IsNullOrEmpty(options.StatusFile.Path))
            {
                var statusResponse = (ResultRangeFetched)fetchReplies[fetchIndex++];
                var parseResult = ReadStatus(statusResponse, options.StatusFile, runner.GetInitialFileTimestamp(options.StatusFile.Path));
                if (!parseResult.TryGetResult(out statusString, out var parseError))
                    return new DebugRunResult(result, parseError, null);
            }
            var outputResponse = (MetadataFetched)fetchReplies[fetchIndex];
            var dataResult = ReadOutput(outputResponse, options.OutputFile, runner.GetInitialFileTimestamp(options.OutputFile.Path), watches, options.BinaryOutput, options.OutputOffset);
            if (!dataResult.TryGetResult(out var data, out var error))
                return new DebugRunResult(result, error, null);

            return new DebugRunResult(result, null, new BreakState(data, execTimer.ElapsedMilliseconds, statusString));
        }

        private static Result<ReadOnlyCollection<string>> ReadValidWatches(ResultRangeFetched response, BuiltinActionFile file, DateTime initTimestamp)
        {
            if (response.Status == FetchStatus.FileNotFound)
                return new Error($"Valid watches file ({file.Path}) could not be found.", title: "Valid watches file is missing");
            if (file.CheckTimestamp && response.Timestamp == initTimestamp)
                return new Error($"Valid watches file ({file.Path}) was not modified.", title: "Data may be stale");

            var watches = Encoding.UTF8.GetString(response.Data).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return Array.AsReadOnly(watches);
        }

        private static Result<string> ReadStatus(ResultRangeFetched response, BuiltinActionFile file, DateTime initTimestamp)
        {
            if (response.Status == FetchStatus.FileNotFound)
                return new Error($"Status string file ({file.Path}) could not be found.", title: "Status string file is missing");
            if (file.CheckTimestamp && response.Timestamp == initTimestamp)
                return new Error($"Status string file ({file.Path}) was not modified.", title: "Data may be stale");

            return Encoding.UTF8.GetString(response.Data).Replace("\n", "\r\n");
        }

        private static Result<BreakStateData> ReadOutput(MetadataFetched response, BuiltinActionFile file, DateTime initTimestamp, ReadOnlyCollection<string> watches, bool binaryOutput, int outputOffset)
        {
            if (response.Status == FetchStatus.FileNotFound)
                return new Error($"Output file ({file.Path}) could not be found.", title: "Output file is missing");
            if (file.CheckTimestamp && response.Timestamp == initTimestamp)
                return new Error($"Output file ({file.Path}) was not modified.", title: "Data may be stale");

            // TODO: refactor OutputFile away
            var output = new OutputFile(directory: "", file: file.Path, binaryOutput);
            return new BreakStateData(watches, output, response.Timestamp, response.ByteCount, outputOffset);
        }
    }
}
