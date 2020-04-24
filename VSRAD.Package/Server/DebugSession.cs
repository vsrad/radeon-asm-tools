using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    internal sealed class DebugSession
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly IFileSynchronizationManager _fileSynchronizationManager;
        private readonly RemoteCommandExecutor _remoteExecutor;

        private Stopwatch _timer;

        public DebugSession(
            IProject project,
            ICommunicationChannel channel,
            IFileSynchronizationManager fileSynchronizationManager,
            IOutputWindowManager outputWindowManager)
        {
            _project = project;
            _channel = channel;
            _fileSynchronizationManager = fileSynchronizationManager;
            _remoteExecutor = new RemoteCommandExecutor("Debugger", channel, outputWindowManager);
            _timer = new Stopwatch();
        }

        public async Task<Result<BreakState>> ExecuteAsync(uint[] breakLines, ReadOnlyCollection<string> watches)
        {
            _timer.Restart();
            var evaluator = await _project.GetMacroEvaluatorAsync(breakLines).ConfigureAwait(false);
            var options = await _project.Options.Profile.Debugger.EvaluateAsync(evaluator).ConfigureAwait(false);
            var outputFile = options.RemoteOutputFile;

            var initOutputTimestamp = (await GetMetadataAsync(outputFile).ConfigureAwait(false)).Timestamp;
            var initValidWatchesTimestamp = options.ParseValidWatches
                ? (await GetMetadataAsync(options.ValidWatchesFile).ConfigureAwait(false)).Timestamp
                : default;

            var initStatusStringTimestamp = !string.IsNullOrEmpty(options.StatusStringFilePath)
                ? (await GetMetadataAsync(options.ValidWatchesFile).ConfigureAwait(false)).Timestamp
                : default;

            await _fileSynchronizationManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var executionResult = await _remoteExecutor.ExecuteAsync(
                new Execute
                {
                    Executable = options.Executable,
                    Arguments = options.Arguments,
                    RunAsAdministrator = options.RunAsAdmin,
                    ExecutionTimeoutSecs = options.TimeoutSecs,
                    WorkingDirectory = options.RemoteOutputFile.Directory
                }).ConfigureAwait(false);

            if (!executionResult.TryGetResult(out var resultData, out var error))
                return error;

            if (options.ParseValidWatches)
            {
                var validWatchesResult = await GetValidWatchesAsync(initValidWatchesTimestamp, options.ValidWatchesFile).ConfigureAwait(false);
                if (!validWatchesResult.TryGetResult(out watches, out error))
                    return error;
            }

            var statusString = "";

            if (!string.IsNullOrEmpty(options.StatusStringFilePath))
            {
                var statusStringResult = await GetStatusString(initStatusStringTimestamp, options.StatusStringFile);
                if (!statusStringResult.TryGetResult(out statusString, out error))
                    return error;
            }

            return await CreateBreakStateAsync(options.RemoteOutputFile, initOutputTimestamp, watches, resultData.ExecutionTime, statusString);
        }

        private async Task<Result<ReadOnlyCollection<string>>> GetValidWatchesAsync(DateTime initValidWatchesTimestamp, Options.OutputFile validWatchesFile)
        {
            var validWatchesMetadata = await GetMetadataAsync(validWatchesFile).ConfigureAwait(false);
            if (validWatchesMetadata.Status != FetchStatus.Successful)
                return new Error($"Valid watches file ({validWatchesFile.File}) could not be found.", title: "Valid watches file is missing");
            if (validWatchesMetadata.Timestamp == initValidWatchesTimestamp)
                return new Error($"Valid watches file ({validWatchesFile.File}) was not modified.", title: "Data may be stale");

            var validWatchesData = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                new FetchResultRange { FilePath = validWatchesFile.Path, BinaryOutput = validWatchesFile.BinaryOutput }).ConfigureAwait(false);
            if (validWatchesData.Status != FetchStatus.Successful)
                return new Error($"Valid watches file ({validWatchesFile.File}) could not be opened.");

            var validWatches = System.Text.Encoding.Default.GetString(validWatchesData.Data)
                .Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            return Array.AsReadOnly(validWatches);
        }

        private async Task<Result<string>> GetStatusString(DateTime initStatusStringFileTimeStamp, Options.OutputFile statusStringFile)
        {
            var statusStringFileMetadata = await GetMetadataAsync(statusStringFile).ConfigureAwait(false);
            if (statusStringFileMetadata.Status != FetchStatus.Successful)
                return new Error($"Status string file ({statusStringFile.File}) could not be found.", title: "Status string file is missing");
            if (statusStringFileMetadata.Timestamp == initStatusStringFileTimeStamp)
                return new Error($"Status string file ({statusStringFile.File}) was not modified.", title: "Data may be stale");

            var statusStringData = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                new FetchResultRange { FilePath = statusStringFile.Path, BinaryOutput = statusStringFile.BinaryOutput }).ConfigureAwait(false);
            if (statusStringData.Status != FetchStatus.Successful)
                return new Error($"Status string file ({statusStringFile.File}) could not be opened.");

            var statusString = System.Text.Encoding.Default.GetString(statusStringData.Data)
                .Replace("\r\n", "\n");

            return statusString;
        }

        private async Task<Result<BreakState>> CreateBreakStateAsync(Options.OutputFile output, DateTime initOutputTimestamp, ReadOnlyCollection<string> watches, long execElapsedMilliseconds, string statusString)
        {
            var metadataResponse = await GetMetadataAsync(output).ConfigureAwait(false);
            if (metadataResponse.Status != FetchStatus.Successful)
                return new Error($"Output file ({output.File}) could not be found.", title: "Output file is missing");
            if (metadataResponse.Timestamp == initOutputTimestamp)
                return new Error($"Output file ({output.File}) was not modified.", title: "Data may be stale");

            _timer.Stop();
            return new BreakState(output, metadataResponse.Timestamp, (uint)metadataResponse.ByteCount,
                _project.Options.Profile.Debugger.OutputOffset, watches, _channel, _timer.ElapsedMilliseconds, execElapsedMilliseconds, statusString);
        }

        private Task<MetadataFetched> GetMetadataAsync(Options.OutputFile file) =>
            _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = file.Path, BinaryOutput = file.BinaryOutput });
    }
}
