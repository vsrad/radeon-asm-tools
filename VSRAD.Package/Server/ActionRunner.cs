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

    public sealed class ActionRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();
        private readonly ActionEnvironment _environment;
        private readonly IProject _project;

        public ActionRunner(ICommunicationChannel channel, SVsServiceProvider serviceProvider, ActionEnvironment environment, IProject project)
        {
            _channel = channel;
            _serviceProvider = serviceProvider;
            _environment = environment;
            _project = project;
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

                StepResult result;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        result = await DoCopyFileAsync(copyFile, cancellationToken);
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

        private async Task<StepResult> DoCopyFileAsync(CopyFileStep step, CancellationToken cancellationToken)
        {
            // Fast path for RemoteToLocal or LocalToRemote copies that are forced to LocalToLocal copies by the "Run on Localhost" option
            if (step.Direction == FileCopyDirection.LocalToLocal && step.SourcePath == step.TargetPath)
            {
                return new StepResult(true, "", "No files copied. The source and target locations are identical.\r\n");
            }
            IList<FileMetadata> sourceFiles, targetFiles;
            // List all source files
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new ListFilesCommand { Path = step.SourcePath, IncludeSubdirectories = step.IncludeSubdirectories };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, cancellationToken);
                sourceFiles = response.Files;
            }
            else
            {
                if (!TryGetMetadataForLocalPath(step.SourcePath, step.IncludeSubdirectories, out sourceFiles, out var error))
                    return new StepResult(false, error, "");
            }
            if (sourceFiles.Count == 0)
            {
                return new StepResult(false, $"File or directory not found. The source path is missing on the {(step.Direction == FileCopyDirection.RemoteToLocal ? "remote" : "local")} machine: {step.SourcePath}", "");
            }
            // List all target files
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                var command = new ListFilesCommand { Path = step.TargetPath, IncludeSubdirectories = step.IncludeSubdirectories };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, cancellationToken);
                targetFiles = response.Files;
            }
            else
            {
                if (!TryGetMetadataForLocalPath(step.TargetPath, step.IncludeSubdirectories, out targetFiles, out var error))
                    return new StepResult(false, error, "");
            }
            // Copying one file?
            if (sourceFiles.Count == 1 && !sourceFiles[0].IsDirectory)
            {
                if (targetFiles.Count > 0 && targetFiles[0].IsDirectory)
                    return new StepResult(false, $"File cannot be copied. The target path is a directory: {step.SourcePath}", "");

                if (step.IfNotModified == ActionIfNotModified.Fail && sourceFiles[0].LastWriteTimeUtc == GetInitialFileTimestamp(step.SourcePath))
                    return new StepResult(false, $"File is stale. The source path was not modified on the {(step.Direction == FileCopyDirection.RemoteToLocal ? "remote" : "local")} machine: {step.SourcePath}", "");

                if (step.IfNotModified == ActionIfNotModified.DoNotCopy
                    && targetFiles.Count == 1
                    && sourceFiles[0].Size == targetFiles[0].Size
                    && sourceFiles[0].LastWriteTimeUtc == targetFiles[0].LastWriteTimeUtc)
                    return new StepResult(true, "", "No files were copied. Sizes and modification times are identical on the source and target sides.\r\n");

                return await DoCopySingleFileAsync(step, cancellationToken);
            }
            // Copying a directory?
            return await DoCopyDirectoryAsync(step, sourceFiles, targetFiles, cancellationToken);
        }

        private async Task<StepResult> DoCopyDirectoryAsync(CopyFileStep step, IList<FileMetadata> sourceFiles, IList<FileMetadata> targetFiles, CancellationToken cancellationToken)
        {
            // Retrieve source files
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
                if (!src.IsDirectory && !(step.IfNotModified == ActionIfNotModified.DoNotCopy && SourceIdenticalToTarget(src)))
                    filesToGet.Add(src.RelativePath);
            }
            if (filesToGet.Count > 0)
            {
                if (step.Direction == FileCopyDirection.RemoteToLocal)
                {
                    var command = new GetFilesCommand { RootPath = step.SourcePath, Paths = filesToGet.ToArray(), UseCompression = step.UseCompression };
                    var response = await _channel.SendWithReplyAsync<GetFilesResponse>(command, cancellationToken);
                    if (response.Status == GetFilesStatus.FileNotFound)
                        return new StepResult(false, $"Failed to read remote directory. No entries found at {step.SourcePath}", "Files requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");
                    if (response.Status == GetFilesStatus.PermissionDenied)
                        return new StepResult(false, $"Failed to read remote directory. Access is denied to {step.SourcePath}", "Files requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");
                    if (response.Status == GetFilesStatus.OtherIOError)
                        return new StepResult(false, $"Failed to read remote directory at {step.TargetPath}", "Files requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");

                    files.AddRange(response.Files);
                }
                else
                {
                    files.AddRange(PackedFile.PackFiles(step.SourcePath, filesToGet));
                }
            }
            // Include empty directories
            foreach (var src in sourceFiles)
            {
                // ./ indicates the root directory
                if (src.IsDirectory && src.RelativePath != "./" && !(step.IfNotModified == ActionIfNotModified.DoNotCopy && SourceIdenticalToTarget(src)))
                    files.Add(new PackedFile(Array.Empty<byte>(), src.RelativePath, src.LastWriteTimeUtc));
            }
            // Write source files and directories to the target directory
            if (files.Count == 0)
            {
                return new StepResult(true, "", "No files copied. The source and target sizes and modification times are identical.\r\n");
            }
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                ICommand command = new PutDirectoryCommand { Files = files.ToArray(), Path = step.TargetPath, PreserveTimestamps = step.PreserveTimestamps };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutDirectoryResponse>(command, cancellationToken);
                if (response.Status == PutDirectoryStatus.TargetPathIsFile)
                    return new StepResult(false, $"Failed to copy files to remote directory. The target path is a file: {step.SourcePath}", "");
                if (response.Status == PutDirectoryStatus.PermissionDenied)
                    return new StepResult(false, $"Failed to copy files to remote directory. Access is denied to {step.TargetPath}. Make sure that the path is accessible and not marked as read-only.", "");
                if (response.Status == PutDirectoryStatus.OtherIOError)
                    return new StepResult(false, $"Failed to copy files to remote directory {step.TargetPath}", "");
            }
            else
            {
                if (File.Exists(step.TargetPath))
                {
                    return new StepResult(false, $"Failed to copy files to local directory. The target path is a file: {step.SourcePath}", "");
                }
                try
                {
                    PackedFile.UnpackFiles(step.TargetPath, files, step.PreserveTimestamps);
                }
                catch (UnauthorizedAccessException e)
                {
                    return new StepResult(false, $"Failed to copy files to local directory {step.TargetPath}. {e.Message} Make sure that the path is accessible and not marked as read-only.", "");
                }
                catch (IOException e)
                {
                    return new StepResult(false, $"Failed to copy files to local directory {step.TargetPath}. {e.Message}", "");
                }
            }

            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoCopySingleFileAsync(CopyFileStep step, CancellationToken cancellationToken)
        {
            byte[] sourceContents;
            // Read source file
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new FetchResultRange { FilePath = step.SourcePath };
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(command, cancellationToken);
                if (response.Status == FetchStatus.FileNotFound)
                    return new StepResult(false, $"File not found. Failed to read remote file {step.SourcePath}", "");
                sourceContents = response.Data;
            }
            else
            {
                if (!ReadLocalFile(step.SourcePath, out sourceContents, out var error))
                    return new StepResult(false, error, "");
            }
            // Write target file
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                ICommand command = new PutFileCommand { FilePath = step.TargetPath, Data = sourceContents };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutFileResponse>(command, cancellationToken);
                if (response.Status == PutFileStatus.PermissionDenied)
                    return new StepResult(false, $"Access denied. Failed to write remote file {step.TargetPath}. Make sure that the path is accessible and not marked as read-only.", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"Failed to write remote file {step.TargetPath}", "");
            }
            else
            {
                if (!WriteLocalFile(step.TargetPath, sourceContents, out var error))
                    return new StepResult(false, error, "");
            }

            return new StepResult(true, "", "");
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
            VsEditor.OpenFileInEditor(_serviceProvider, step.Path, null, step.LineMarker,
                _project.Options.DebuggerOptions.ForceOppositeTab, _project.Options.DebuggerOptions.PreserveActiveDoc);
            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoRunActionAsync(RunActionStep step, bool continueOnError, CancellationToken cancellationToken)
        {
            var subActionResult = await RunAsync(step.Name, step.EvaluatedSteps, continueOnError, cancellationToken);
            return new StepResult(subActionResult.Successful, "", "", subAction: subActionResult);
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
                var result = await ReadDebugDataFileAsync("Valid watches", step.WatchesFile.Path, step.WatchesFile.IsRemote(), step.WatchesFile.CheckTimestamp, cancellationToken);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                validWatchesString = Encoding.UTF8.GetString(data);
            }
            string dispatchParamsString;
            {
                var result = await ReadDebugDataFileAsync("Dispatch parameters", step.DispatchParamsFile.Path, step.DispatchParamsFile.IsRemote(), step.DispatchParamsFile.CheckTimestamp, cancellationToken);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                dispatchParamsString = Encoding.UTF8.GetString(data);
            }
            {
                var outputPath = step.OutputFile.Path;
                var initOutputTimestamp = GetInitialFileTimestamp(outputPath);

                BreakStateOutputFile outputFile;
                byte[] localOutputData = null;

                if (step.OutputFile.IsRemote())
                {
                    var response = await _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = outputPath, BinaryOutput = step.BinaryOutput }, cancellationToken);

                    if (response.Status == FetchStatus.FileNotFound)
                        return new StepResult(false, $"Debug data is missing. Output file could not be found on the remote machine at {outputPath}", "", breakState: null);
                    if (step.OutputFile.CheckTimestamp && response.Timestamp == initOutputTimestamp)
                        return new StepResult(false, $"Debug data is stale. Output file was not modified by the debug action on the remote machine at {outputPath}", "", breakState: null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var dataDwordCount = Math.Max(0, (response.ByteCount - offset) / 4);
                    outputFile = new BreakStateOutputFile(outputPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dataDwordCount);
                }
                else
                {
                    var timestamp = GetLocalFileLastWriteTimeUtc(outputPath);
                    if (step.OutputFile.CheckTimestamp && timestamp == initOutputTimestamp)
                        return new StepResult(false, $"Debug data is stale. Output file was not modified by the debug action on the local machine at {outputPath}", "", breakState: null);

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

        private async Task<Result<byte[]>> ReadDebugDataFileAsync(string type, string path, bool isRemote, bool checkTimestamp, CancellationToken cancellationToken)
        {
            var initTimestamp = GetInitialFileTimestamp(path);
            if (isRemote)
            {
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = path }, cancellationToken);

                if (response.Status == FetchStatus.FileNotFound)
                    return new Error($"{type} data is missing. File could not be found on the remote machine at {path}");
                if (checkTimestamp && response.Timestamp == initTimestamp)
                    return new Error($"{type} data is stale. File was not modified by the debug action on the remote machine at {path}");

                return response.Data;
            }
            else
            {
                if (checkTimestamp && GetLocalFileLastWriteTimeUtc(path) == initTimestamp)
                    return new Error($"{type} data is stale. File was not modified by the debug action on the local machine at {path}");
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
                error = $"Access denied. Failed to write local file {fullPath}. Make sure that the path is accessible and not marked as read-only.";
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
                if (step is CopyFileStep copyFile && copyFile.IfNotModified == ActionIfNotModified.Fail)
                {
                    if (copyFile.Direction == FileCopyDirection.RemoteToLocal)
                        _initialTimestamps[copyFile.SourcePath] = (await _channel.SendWithReplyAsync<MetadataFetched>(
                            new FetchMetadata { FilePath = copyFile.SourcePath }, cancellationToken)).Timestamp;
                    else
                        _initialTimestamps[copyFile.SourcePath] = GetLocalFileLastWriteTimeUtc(copyFile.SourcePath);
                }
                else if (step is ReadDebugDataStep readDebugData)
                {
                    var files = new[] { readDebugData.WatchesFile, readDebugData.DispatchParamsFile, readDebugData.OutputFile };
                    foreach (var file in files)
                    {
                        if (!file.CheckTimestamp || string.IsNullOrEmpty(file.Path))
                            continue;
                        if (file.IsRemote())
                            _initialTimestamps[file.Path] = (await _channel.SendWithReplyAsync<MetadataFetched>(
                                new FetchMetadata { FilePath = file.Path }, cancellationToken)).Timestamp;
                        else
                            _initialTimestamps[file.Path] = GetLocalFileLastWriteTimeUtc(file.Path);
                    }
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

        private static bool TryGetMetadataForLocalPath(string localPath, bool includeSubdirectories, out IList<FileMetadata> metadata, out string error)
        {
            try
            {
                error = "";
                metadata = FileMetadata.GetMetadataForPath(localPath, includeSubdirectories);
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                error = $"Failed to access metadata for local path {localPath}. {e.Message}";
                metadata = null;
                return false;
            }
        }
    }
}
