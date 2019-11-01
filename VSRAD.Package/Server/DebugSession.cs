using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Server
{
    public delegate void DebugBreakEntered(BreakState breakState);
    public delegate bool DebugSessionTryPopRunToLine(string file, out uint runToLine);
    public delegate ReadOnlyCollection<string> DebugSessionGetCurrentWatches();

    internal sealed class DebugSession : IEngineIntegration
    {
        public event Action RerunRequested;
        public event ExecutionCompleted ExecutionCompleted;

        private readonly IProject _project;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly ICommunicationChannel _channel;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly DebugSessionGetCurrentWatches _getWatches;
        private readonly DebugSessionTryPopRunToLine _popRunToLine;
        private readonly DebugBreakEntered _onBreakEntered;

        private readonly IOutputWindowWriter _outputWriter;

        public DebugSession(
            IProject project,
            IActiveCodeEditor codeEditor,
            ICommunicationChannel channel,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindowManager,
            DebugSessionGetCurrentWatches getWatches,
            DebugSessionTryPopRunToLine popRunToLine,
            DebugBreakEntered breakEnteredCallback)
        {
            _project = project;
            _codeEditor = codeEditor;
            _channel = channel;
            _deployManager = deployManager;
            _getWatches = getWatches;
            _popRunToLine = popRunToLine;
            _onBreakEntered = breakEnteredCallback;

            _outputWriter = outputWindowManager.GetExecutionResultPane();
        }

        internal void RequestRerun() => RerunRequested();

        void IEngineIntegration.ExecuteToLine(uint breakLine)
        {
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var watches = _getWatches();
                var evaluator = await _project.GetMacroEvaluatorAsync(breakLine).ConfigureAwait(false);
                var options = await _project.Options.Profile.Debugger.EvaluateAsync(evaluator).ConfigureAwait(false);

                var initValidWatchesFileTimestamp = options.ParseValidWatches 
                    ? await GetOutputTimestampAsync(options.RemoteOutputFile).ConfigureAwait(false) 
                    : DateTime.MinValue;
                var initOutputTimestamp = await GetOutputTimestampAsync(options.RemoteOutputFile).ConfigureAwait(false);
                var executionResult = await ExecuteAsync(options, breakLine).ConfigureAwait(false);
                await _outputWriter.PrintExecutionResultAsync("Debugger", executionResult).ConfigureAwait(false);

                if (options.ParseValidWatches)
                    watches = await GetValidWatchesAsync(initValidWatchesFileTimestamp, options.ValidWatchesFile);

                await VSPackage.TaskFactory.SwitchToMainThreadAsync();

                if (executionResult.Status == DebugServer.IPC.Responses.ExecutionStatus.Completed && executionResult.ExitCode == 0)
                {
                    var breakState = await CreateBreakStateAsync(options.RemoteOutputFile, initOutputTimestamp, watches);
                    if (breakState != null)
                    {
                        ExecutionCompleted(success: true);
                        _onBreakEntered(breakState);
                    }
                    else
                    {
                        ExecutionCompleted(success: false);
                    }
                    return;
                }
                switch (executionResult.Status)
                {
                    case DebugServer.IPC.Responses.ExecutionStatus.Completed:
                        Errors.ShowWarning($"Execution has finished with a non-zero exit code ({executionResult.ExitCode})");
                        break;
                    case DebugServer.IPC.Responses.ExecutionStatus.TimedOut:
                        Errors.ShowWarning("Debugger execution has timed out");
                        break;
                    case DebugServer.IPC.Responses.ExecutionStatus.CouldNotLaunch:
                        Errors.ShowCritical("Could not launch the process. Check the executable path in project settings and file permissions");
                        break;
                }
                ExecutionCompleted(success: false);
            },
            exceptionCallbackOnMainThread: () => ExecutionCompleted(success: false));
        }

        private async Task<ReadOnlyCollection<string>> GetValidWatchesAsync(DateTime initValidWatchesFileTimestamp, Options.OutputFile validWatchesFile)
        {
            var validWatchesFileMetadata = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.MetadataFetched>(
                new DebugServer.IPC.Commands.FetchMetadata
                {
                    FilePath = validWatchesFile.Path,
                    BinaryOutput = true
                }).ConfigureAwait(false);
            if (validWatchesFileMetadata.Status == DebugServer.IPC.Responses.FetchStatus.FileNotFound)
            {
                Errors.ShowWarning($"Cannot find file ({string.Join(Path.DirectorySeparatorChar.ToString(), validWatchesFile.Path)})");
                return new ReadOnlyCollection<string>(new List<string>());
            }
            if (initValidWatchesFileTimestamp == validWatchesFileMetadata.Timestamp)
                Errors.ShowWarning($"Valid watches file was not updated during last debugger invocation: data may be stale");
            var validWatchesData = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.ResultRangeFetched>(
                new DebugServer.IPC.Commands.FetchResultRange
                {
                    FilePath = validWatchesFile.Path,
                    BinaryOutput = true,
                    ByteCount = 0,
                    ByteOffset = 0
                }).ConfigureAwait(false);
            if (validWatchesData.Status != DebugServer.IPC.Responses.FetchStatus.Successful)
                throw new Exception("Output file could not be opened.");

            var validWatchesText = System.Text.Encoding.Default.GetString(validWatchesData.Data);

            return Array.AsReadOnly(validWatchesText.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
        }

        string IEngineIntegration.GetActiveProjectFile() =>
            _project.GetRelativePath(_codeEditor.GetAbsoluteSourcePath());

        uint IEngineIntegration.GetFileLineCount(string projectFilePath) =>
            (uint)File.ReadLines(_project.GetAbsolutePath(projectFilePath)).Count();

        string IEngineIntegration.GetProjectRelativePath(string path) =>
            _project.GetRelativePath(path);

        bool IEngineIntegration.PopRunToLineIfSet(string file, out uint runToLine) =>
            _popRunToLine(file, out runToLine);

        private async Task<DebugServer.IPC.Responses.ExecutionCompleted> ExecuteAsync(Options.DebuggerProfileOptions options, uint breakLine)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);
            var response = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.ExecutionCompleted>(
                new DebugServer.IPC.Commands.Execute
                {
                    Executable = options.Executable,
                    Arguments = options.Arguments,
                    RunAsAdministrator = options.RunAsAdmin,
                    ExecutionTimeoutSecs = options.TimeoutSecs,
                    WorkingDirectory = options.RemoteOutputFile.Directory
                }).ConfigureAwait(false);
            return response;
        }

        private async Task<DateTime> GetOutputTimestampAsync(Options.OutputFile outputFile)
        {
            var response = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.MetadataFetched>(
                new DebugServer.IPC.Commands.FetchMetadata
                {
                    FilePath = outputFile.Path,
                    BinaryOutput = outputFile.BinaryOutput
                }).ConfigureAwait(false);
            /* We don't check the status because if the file could not be found the response contains DateTime.MinValue */
            return response.Timestamp;
        }

        private async Task<BreakState> CreateBreakStateAsync(Options.OutputFile outputFile, DateTime initialOutputTimestamp, ReadOnlyCollection<string> watches)
        {
            var metadataResponse = await _channel.SendWithReplyAsync<DebugServer.IPC.Responses.MetadataFetched>(
                new DebugServer.IPC.Commands.FetchMetadata
                {
                    FilePath = outputFile.Path,
                    BinaryOutput = outputFile.BinaryOutput
                }).ConfigureAwait(false);
            if (metadataResponse.Status == DebugServer.IPC.Responses.FetchStatus.Successful)
            {
                if (metadataResponse.Timestamp == initialOutputTimestamp)
                {
                    await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                    Errors.ShowWarning($"Output file ({string.Join(Environment.NewLine, outputFile.Path)}) has not been modified.", "Data may be stale");
                }
                return new BreakState(outputFile,
                    metadataResponse.Timestamp,
                    (uint)metadataResponse.ByteCount,
                    _project.Options.Profile.Debugger.OutputOffset,
                    watches, _channel);
            }
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            Errors.ShowWarning($"Output file ({string.Join(Environment.NewLine, outputFile.Path)}) could not be found.", "Output file is missing");
            return null;
        }
    }
}
