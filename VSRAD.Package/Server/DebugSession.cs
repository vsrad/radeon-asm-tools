using Microsoft.VisualStudio.Shell;
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
        private readonly IFileSynchronizationManager _deployManager;
        private readonly SVsServiceProvider _serviceProvider;

        public DebugSession(IProject project, ICommunicationChannel channel, IFileSynchronizationManager deployManager, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _channel = channel;
            _deployManager = deployManager;
            _serviceProvider = serviceProvider;
        }

        public async Task<DebugRunResult> ExecuteAsync(uint[] breakLines, ReadOnlyCollection<string> watches)
        {
            var execTimer = Stopwatch.StartNew();
            var evaluator = await _project.GetMacroEvaluatorAsync(breakLines).ConfigureAwait(false);

            var envResult = await _project.Options.Profile.General.EvaluateActionEnvironmentAsync(evaluator).ConfigureAwait(false);
            if (!envResult.TryGetResult(out var env, out var evalError))
                return new DebugRunResult(null, evalError, null);
            var optionsResult = await _project.Options.Profile.Debugger.EvaluateAsync(evaluator, _project.Options.Profile).ConfigureAwait(false);
            if (!optionsResult.TryGetResult(out var options, out evalError))
                return new DebugRunResult(null, evalError, null);
            if (ValidateConfiguration(options) is Error configError)
                return new DebugRunResult(null, configError, null);

            await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var runner = new ActionRunner(_channel, _serviceProvider, env);
            var auxFiles = new[] { options.OutputFile, options.WatchesFile, options.StatusFile };
            var result = await runner.RunAsync(ActionProfileOptions.BuiltinActionDebug, options.Steps, auxFiles).ConfigureAwait(false);

            if (!result.Successful && !_project.Options.Profile.General.ContinueActionExecOnError)
                return new DebugRunResult(result, null, null);

            var fetchCommands = new List<ICommand>();
            if (options.WatchesFile.IsRemote() && !string.IsNullOrEmpty(options.WatchesFile.Path))
                fetchCommands.Add(new FetchResultRange { FilePath = new[] { env.RemoteWorkDir, options.WatchesFile.Path } });
            if (options.StatusFile.IsRemote() && !string.IsNullOrEmpty(options.StatusFile.Path))
                fetchCommands.Add(new FetchResultRange { FilePath = new[] { env.RemoteWorkDir, options.StatusFile.Path } });
            if (options.OutputFile.IsRemote())
                fetchCommands.Add(new FetchMetadata { FilePath = new[] { env.RemoteWorkDir, options.OutputFile.Path } });

            IResponse[] fetchReplies = null;
            if (fetchCommands.Count > 0)
                fetchReplies = await _channel.SendBundleAsync(fetchCommands);

            var fetchIndex = 0;
            if (!string.IsNullOrEmpty(options.WatchesFile.Path))
            {
                var initTimestamp = runner.GetInitialFileTimestamp(options.WatchesFile.Path);
                var response = (ResultRangeFetched)fetchReplies[fetchIndex++];
                var text = ReadTextFile("Valid watches", options.WatchesFile, response, initTimestamp);
                if (!text.TryGetResult(out var watchesString, out var error))
                    return new DebugRunResult(result, error, null);

                var watchArray = watchesString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                watches = Array.AsReadOnly(watchArray);
            }
            var statusString = "";
            if (!string.IsNullOrEmpty(options.StatusFile.Path))
            {
                var initTimestamp = runner.GetInitialFileTimestamp(options.StatusFile.Path);
                var response = (ResultRangeFetched)fetchReplies[fetchIndex++];
                var text = ReadTextFile("Status string", options.StatusFile, response, initTimestamp);
                if (!text.TryGetResult(out statusString, out var error))
                    return new DebugRunResult(result, error, null);

                statusString = statusString.Replace("\n", "\r\n");
            }
            {
                var initTimestamp = runner.GetInitialFileTimestamp(options.OutputFile.Path);
                var response = (MetadataFetched)fetchReplies[fetchIndex++];
                var metadata = ReadOutputMetadata(options.OutputFile, response, initTimestamp);
                if (!metadata.TryGetResult(out var outputMeta, out var error))
                    return new DebugRunResult(result, error, null);

                // TODO: refactor OutputFile away
                var output = new OutputFile(directory: env.RemoteWorkDir, file: options.OutputFile.Path, options.BinaryOutput);
                var data = new BreakStateData(watches, output, outputMeta.timestamp, outputMeta.byteCount, options.OutputOffset);
                return new DebugRunResult(result, null, new BreakState(data, execTimer.ElapsedMilliseconds, statusString));
            }
        }

        private static Error? ValidateConfiguration(DebuggerProfileOptions options)
        {
            if (string.IsNullOrEmpty(options.OutputFile.Path))
                return new Error("Debugger output path is not specified. To set it, go to Tools -> RAD Debug -> Options and edit your current profile.");
            if (!options.OutputFile.IsRemote() || !options.WatchesFile.IsRemote() || !options.StatusFile.IsRemote())
                return new Error("Local debugger output paths are not supported in this version of RAD Debugger.");
            return null;
        }

        private static Result<string> ReadTextFile(string title, BuiltinActionFile file, ResultRangeFetched response, DateTime initTimestamp)
        {
            if (file.IsRemote())
            {
                if (response.Status == FetchStatus.FileNotFound)
                    return new Error($"{title} file ({file.Path}) could not be found.", title: $"{title} file is missing");
                if (file.CheckTimestamp && response.Timestamp == initTimestamp)
                    return new Error($"{title} file ({file.Path}) was not modified.", title: "Data may be stale");

                return Encoding.UTF8.GetString(response.Data);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static Result<(DateTime timestamp, int byteCount)> ReadOutputMetadata(BuiltinActionFile file, MetadataFetched response, DateTime initTimestamp)
        {
            if (file.IsRemote())
            {
                if (response.Status == FetchStatus.FileNotFound)
                    return new Error($"Output file ({file.Path}) could not be found.", title: "Output file is missing");
                if (file.CheckTimestamp && response.Timestamp == initTimestamp)
                    return new Error($"Output file ({file.Path}) was not modified.", title: "Data may be stale");

                return (response.Timestamp, response.ByteCount);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
