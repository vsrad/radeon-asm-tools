using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Server
{
    public sealed class ActionEnvironment
    {
        public IReadOnlyList<string> Watches { get; }
        public Result<BreakTarget> BreakTarget { get; }

        public ActionEnvironment(IReadOnlyList<string> watches = null, Result<BreakTarget> breakTarget = null)
        {
            Watches = watches ?? Array.Empty<string>();
            BreakTarget = breakTarget ?? ProjectSystem.BreakTarget.Empty;
        }
    }

    public interface IActionRunnerCallbacks
    {
        void OnNextStepStarted();
        void OnOpenFileInEditorRequested(string filePath, string lineMarker);
    }

    public sealed class ActionRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly IActionRunnerCallbacks _callbacks;
        private readonly ActionEnvironment _environment;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();

        public ActionRunner(ICommunicationChannel channel, IActionRunnerCallbacks callbacks, ActionEnvironment environment)
        {
            _channel = channel;
            _callbacks = callbacks;
            _environment = environment;
        }

        public DateTime GetInitialFileTimestamp(string file) =>
            _initialTimestamps.TryGetValue(file, out var timestamp) ? timestamp : default;

        public async Task<ActionRunResult> RunAsync(string actionName, IReadOnlyList<IActionStep> steps, bool continueOnError = true, CancellationToken cancellationToken = default)
        {
            var runStats = new ActionRunResult(actionName, steps, continueOnError);

            await FillInitialTimestampsAsync(steps, cancellationToken);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < steps.Count; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _callbacks.OnNextStepStarted();

                StepResult result;
                switch (steps[i])
                {
                    case CopyStep copy:
                        result = await DoCopyAsync(copy, cancellationToken);
                        break;
                    case ExecuteStep execute:
                        result = await DoExecuteAsync(execute, cancellationToken);
                        break;
                    case OpenInEditorStep openInEditor:
                        result = await DoOpenInEditorAsync(openInEditor);
                        break;
                    case RunActionStep runAction:
                        result = await DoRunActionAsync(runAction, continueOnError, cancellationToken);
                        break;
                    case WriteDebugTargetStep writeDebugTarget:
                        result = await DoWriteDebugTargetAsync(writeDebugTarget);
                        break;
                    case ReadDebugDataStep readDebugData:
                        result = await DoReadDebugDataAsync(readDebugData, cancellationToken);
                        break;
                    case VerifyFileModifiedStep verifyFileModified:
                        result = await DoVerifyFileModifiedAsync(verifyFileModified, cancellationToken);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                runStats.RecordStep(i, result);
                if (!result.Successful && !continueOnError)
                    break;
            }

            runStats.FinishRun();
            return runStats;
        }

        private async Task<StepResult> DoCopyAsync(CopyStep step, CancellationToken cancellationToken)
        {
            // Fast path for RemoteToLocal or LocalToRemote copies that are forced to LocalToLocal copies by the "Run on Localhost" option
            if (step.Direction == CopyDirection.LocalToLocal && step.SourcePath == step.TargetPath)
            {
                return new StepResult(true, "", "Copy skipped. The source and target locations are identical.\r\n");
            }
            IList<FileMetadata> sourceFiles, targetFiles;
            // List source files
            if (step.Direction == CopyDirection.RemoteToLocal)
            {
                var command = new ListFilesCommand { RootPath = step.SourcePath, Globs = step.GlobsToCopyArray };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, cancellationToken);
                sourceFiles = response.Files;
            }
            else
            {
                if (!TryCollectMetadataForLocalPath(step.SourcePath, step.GlobsToCopyArray, out sourceFiles, out var error))
                    return new StepResult(false, error, "");
            }
            if (sourceFiles.Count == 0)
            {
                return new StepResult(false, $"The source file or directory is missing on the {(step.Direction == CopyDirection.RemoteToLocal ? "remote" : "local")} machine at {step.SourcePath}", "");
            }
            // List target files
            if (step.Direction == CopyDirection.LocalToRemote)
            {
                var command = new ListFilesCommand { RootPath = step.TargetPath, Globs = step.GlobsToCopyArray };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, cancellationToken);
                targetFiles = response.Files;
            }
            else
            {
                if (!TryCollectMetadataForLocalPath(step.TargetPath, step.GlobsToCopyArray, out targetFiles, out var error))
                    return new StepResult(false, error, "");
            }
            // Get source file contents
            var files = new List<PackedFile>();
            bool SourceIdenticalToTarget(FileMetadata src)
            {
                foreach (var dst in targetFiles)
                {
                    if (dst.RelativePath == src.RelativePath)
                        return dst.Size == src.Size && dst.LastWriteTimeUtc == src.LastWriteTimeUtc;
                }
                return false;
            }
            var filesToGet = new List<string>();
            foreach (var src in sourceFiles)
            {
                if (src.IsDirectory)
                {
                    // ./ indicates the root directory
                    if (src.RelativePath != "./" && !(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
                        files.Add(new PackedFile(src.RelativePath, src.LastWriteTimeUtc, Array.Empty<byte>()));
                }
                else
                {
                    if (!(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
                        filesToGet.Add(src.RelativePath);
                }
            }
            if (filesToGet.Count > 0)
            {
                var command = new GetFilesCommand { RootPath = step.SourcePath, Paths = filesToGet.ToArray(), UseCompression = step.UseCompression };
                GetFilesResponse response;
                if (step.Direction == CopyDirection.RemoteToLocal)
                    response = await _channel.SendWithReplyAsync<GetFilesResponse>(command, cancellationToken);
                else
                    response = FileTransfer.GetFiles(command);

                if (response.Status == GetFilesStatus.Successful)
                    files.AddRange(response.Files);
                else if (response.Status == GetFilesStatus.FileOrDirectoryNotFound)
                    return new StepResult(false, $"Failed to find the requested files on the {(step.Direction == CopyDirection.RemoteToLocal ? "remote" : "local")} machine. {response.ErrorMessage}", "");
                else if (response.Status == GetFilesStatus.PermissionDenied)
                    return new StepResult(false, $"Failed to access the {(step.Direction == CopyDirection.RemoteToLocal ? "remote" : "local")} source path. {response.ErrorMessage}", "");
                else
                    return new StepResult(false, $"Failed to read file(s) from the {(step.Direction == CopyDirection.RemoteToLocal ? "remote" : "local")} source path. {response.ErrorMessage}", "");
            }
            // Write source files and directories to the target directory
            if (files.Count > 0)
            {
                var command = new PutFilesCommand { Files = files.ToArray(), RootPath = step.TargetPath, PreserveTimestamps = step.PreserveTimestamps };
                PutFilesResponse response;
                if (step.Direction == CopyDirection.LocalToRemote)
                    response = await _channel.SendWithReplyAsync<PutFilesResponse>(step.UseCompression ? new CompressedCommand(command) : (ICommand)command, cancellationToken);
                else
                    response = await FileTransfer.PutFilesAsync(command);

                if (response.Status == PutFilesStatus.Successful)
                    return new StepResult(true, "", "");
                else if (response.Status == PutFilesStatus.PermissionDenied)
                    return new StepResult(false, $"Failed to access the {(step.Direction == CopyDirection.LocalToRemote ? "remote" : "local")} target path. {response.ErrorMessage} Make sure that the path is not marked as read-only.", "");
                else
                    return new StepResult(false, $"Failed to write file(s) to the {(step.Direction == CopyDirection.LocalToRemote ? "remote" : "local")} target path. {response.ErrorMessage}", "");
            }
            else
            {
                return new StepResult(true, "", "Copy skipped. The source and target sizes and modification times are identical.\r\n");
            }
        }

        private async Task<StepResult> DoExecuteAsync(ExecuteStep step, CancellationToken cancellationToken)
        {
            var command = new Execute
            {
                Executable = step.Executable,
                Arguments = step.Arguments,
                WorkingDirectory = step.WorkingDirectory,
                RunAsAdministrator = step.RunAsAdmin,
                WaitForCompletion = step.WaitForCompletion,
                ExecutionTimeoutSecs = step.TimeoutSecs
            };
            ExecutionCompleted response;
            if (step.Environment == StepEnvironment.Local)
                response = await new ObservableProcess(command).StartAndObserveAsync(cancellationToken);
            else
                response = await _channel.SendWithReplyAsync<ExecutionCompleted>(command, cancellationToken);

            var log = new StringBuilder();
            var status = response.Status == ExecutionStatus.Completed ? $"exit code {response.ExitCode}"
                       : response.Status == ExecutionStatus.TimedOut ? "timed out"
                       : "could not launch";
            var stdout = response.Stdout.TrimEnd('\r', '\n');
            var stderr = response.Stderr.TrimEnd('\r', '\n');
            if (stdout.Length == 0 && stderr.Length == 0)
                log.AppendFormat("No stdout/stderr captured ({0})\r\n", status);
            if (stdout.Length != 0)
                log.AppendFormat("Captured stdout ({0}):\r\n{1}\r\n", status, stdout);
            if (stderr.Length != 0)
                log.AppendFormat("Captured stderr ({0}):\r\n{1}\r\n", status, stderr);

            var machine = step.Environment == StepEnvironment.Local ? "local" : "remote";
            switch (response.Status)
            {
                case ExecutionStatus.Completed when response.ExitCode == 0:
                    return new StepResult(true, "", log.ToString(), errorListOutput: new string[] { stdout, stderr });
                case ExecutionStatus.Completed:
                    return new StepResult(false, $"Check the errors in Error List and the command output in Output -> RAD Debug. (The {machine} `{step.Executable}` process exited with non-zero code {response.ExitCode}.)", log.ToString(), errorListOutput: new string[] { stdout, stderr });
                case ExecutionStatus.TimedOut:
                    return new StepResult(false, $"Execution timeout is exceeded. (The {machine} `{step.Executable}` process is terminated.)", log.ToString());
                default:
                    return new StepResult(false, $"Check that the executable is specified correctly. (The {machine} `{step.Executable}` process could not be started.)", log.ToString());
            }
        }

        private async Task<StepResult> DoOpenInEditorAsync(OpenInEditorStep step)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _callbacks.OnOpenFileInEditorRequested(step.Path, step.LineMarker);
            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoRunActionAsync(RunActionStep step, bool continueOnError, CancellationToken cancellationToken)
        {
            if (step.EvaluatedCondition)
            {
                var subActionResult = await RunAsync(step.Name, step.EvaluatedSteps, continueOnError, cancellationToken);
                return new StepResult(subActionResult.Successful, "", "", subAction: subActionResult);
            }
            else
            {
                return new StepResult(true, "", "Action skipped. The action run condition evaluated to false.");
            }
        }

        private Task<StepResult> DoWriteDebugTargetAsync(WriteDebugTargetStep step)
        {
            if (!_environment.BreakTarget.TryGetResult(out var breakpointList, out var error))
                return Task.FromResult(new StepResult(false, error.Message, ""));

            var breakpointListJson = JsonConvert.SerializeObject(breakpointList, Formatting.Indented);
            if (!WriteLocalFile(step.BreakpointListPath, Encoding.UTF8.GetBytes(breakpointListJson), out var errorString))
                return Task.FromResult(new StepResult(false, errorString, ""));

            var watchListLines = string.Join(Environment.NewLine, _environment.Watches);
            if (!WriteLocalFile(step.WatchListPath, Encoding.UTF8.GetBytes(watchListLines), out errorString))
                return Task.FromResult(new StepResult(false, errorString, ""));

            return Task.FromResult(new StepResult(true, "", ""));
        }

        private async Task<StepResult> DoReadDebugDataAsync(ReadDebugDataStep step, CancellationToken cancellationToken)
        {
            BreakTarget breakTarget;
            {
                if (!_environment.BreakTarget.TryGetResult(out breakTarget, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
            }
            string validWatchesString;
            {
                var result = await ReadDebugDataFileAsync("Valid watches", step.WatchesFile.Path, step.WatchesFile.IsRemote(), cancellationToken);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                validWatchesString = Encoding.UTF8.GetString(data);
            }
            string dispatchParamsString;
            {
                var result = await ReadDebugDataFileAsync("Dispatch parameters", step.DispatchParamsFile.Path, step.DispatchParamsFile.IsRemote(), cancellationToken);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                dispatchParamsString = Encoding.UTF8.GetString(data);
            }
            {
                var outputPath = step.OutputFile.Path;

                BreakStateOutputFile outputFile;
                byte[] localOutputData = null;

                if (step.OutputFile.IsRemote())
                {
                    var response = await _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = outputPath, BinaryOutput = step.BinaryOutput }, cancellationToken);

                    if (response.Status == FetchStatus.FileNotFound)
                        return new StepResult(false, $"Debug data is missing. Output file could not be found on the remote machine at {outputPath}", "", breakState: null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var dataDwordCount = Math.Max(0, (response.ByteCount - offset) / 4);
                    outputFile = new BreakStateOutputFile(outputPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dataDwordCount);
                }
                else
                {
                    var timestamp = GetLocalFileLastWriteTimeUtc(outputPath);
                    var readOffset = step.BinaryOutput ? step.OutputOffset : 0;
                    if (!ReadLocalFile(outputPath, out localOutputData, out var readError, readOffset))
                        return new StepResult(false, "Debug data is missing. " + readError, "", breakState: null);
                    if (!step.BinaryOutput)
                        localOutputData = await TextDebuggerOutputParser.ReadTextOutputAsync(new MemoryStream(localOutputData), step.OutputOffset);

                    var dataDwordCount = localOutputData.Length / 4;
                    outputFile = new BreakStateOutputFile(outputPath, step.BinaryOutput, offset: 0, timestamp, dataDwordCount);
                }
                var breakStateResult = BreakState.CreateBreakState(breakTarget, _environment.Watches, validWatchesString, dispatchParamsString, outputFile, localOutputData, step.CheckMagicNumber);
                if (breakStateResult.TryGetResult(out var breakState, out var error))
                    return new StepResult(true, "", "", breakState: breakState);
                else
                    return new StepResult(false, error.Message, "", breakState: null);
            }
        }

        private async Task<StepResult> DoVerifyFileModifiedAsync(VerifyFileModifiedStep step, CancellationToken cancellationToken)
        {
            var initTimestamp = GetInitialFileTimestamp(step.Path);

            DateTime currentTimestamp;
            if (step.Location == StepEnvironment.Local)
            {
                try
                {
                    currentTimestamp = File.GetLastWriteTimeUtc(step.Path);
                }
                catch (Exception e)
                {
                    return new StepResult(false, $"Failed to retrieve last write time of local path {step.Path}. {e.Message}", "");
                }
            }
            else
            {
                var response = await _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = step.Path }, cancellationToken);
                currentTimestamp = response.Timestamp;
            }

            if (initTimestamp == currentTimestamp)
                return new StepResult(successful: !step.AbortIfNotModifed, string.IsNullOrEmpty(step.ErrorMessage) ? $"File is not modified at {step.Path}" : step.ErrorMessage, "");
            else
                return new StepResult(true, "", "");
        }

        private async Task<Result<byte[]>> ReadDebugDataFileAsync(string type, string path, bool isRemote, CancellationToken cancellationToken)
        {
            if (isRemote)
            {
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = path }, cancellationToken);
                if (response.Status == FetchStatus.FileNotFound)
                    return new Error($"{type} data is missing. File could not be found on the remote machine at {path}");
                return response.Data;
            }
            else
            {
                if (!ReadLocalFile(path, out var data, out var error))
                    return new Error($"{type} data is missing. " + error);
                return data;
            }
        }

        private static bool ReadLocalFile(string fullPath, out byte[] data, out string error, int byteOffset = 0)
        {
            try
            {
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
                {
                    stream.Seek(byteOffset, SeekOrigin.Begin);

                    var bytesToRead = Math.Max(0, (int)(stream.Length - stream.Position));
                    data = new byte[bytesToRead];

                    int read = 0, bytesRead = 0;
                    while (bytesRead != bytesToRead)
                    {
                        if ((read = stream.Read(data, 0, bytesToRead - bytesRead)) == 0)
                            throw new IOException("Output file length does not match stream length");
                        bytesRead += read;
                    }
                }
                error = "";
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access denied. Failed to read local file {fullPath}";
            }
            catch (IOException e)
            {
                if (e is FileNotFoundException || e is DirectoryNotFoundException)
                    error = $"File not found. Failed to read local file {fullPath}";
                else
                    error = $"Failed to read local file {fullPath}. {e.Message}";
            }
            data = null;
            return false;
        }

        private static bool WriteLocalFile(string fullPath, byte[] data, out string error)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, data);
                error = "";
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access denied. Failed to write local file {fullPath}. Make sure that the path is not marked as read-only.";
            }
            catch (IOException e)
            {
                error = $"Failed to write local file {fullPath}. {e.Message}";
            }
            return false;
        }

        private async Task FillInitialTimestampsAsync(IReadOnlyList<IActionStep> steps, CancellationToken cancellationToken)
        {
            foreach (var step in steps)
            {
                if (step is VerifyFileModifiedStep verifyFileModified)
                {
                    if (verifyFileModified.Location == StepEnvironment.Local)
                        _initialTimestamps[verifyFileModified.Path] = GetLocalFileLastWriteTimeUtc(verifyFileModified.Path);
                    else
                        _initialTimestamps[verifyFileModified.Path] = (await _channel.SendWithReplyAsync<MetadataFetched>(
                            new FetchMetadata { FilePath = verifyFileModified.Path }, cancellationToken)).Timestamp;
                }
            }
        }

        private static DateTime GetLocalFileLastWriteTimeUtc(string fullPath)
        {
            try
            {
                return File.GetLastWriteTimeUtc(fullPath);
            }
            catch
            {
                return default;
            }
        }

        private static bool TryCollectMetadataForLocalPath(string localPath, string[] globs, out IList<FileMetadata> metadata, out string error)
        {
            try
            {
                error = "";
                metadata = FileMetadata.CollectFileMetadata(localPath, globs);
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                error = $"Failed to access local path {localPath}. {e.Message}";
                metadata = null;
                return false;
            }
        }
    }
}
