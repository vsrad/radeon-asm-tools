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
        public string LocalWorkDir { get; }
        public string RemoteWorkDir { get; }
        public IReadOnlyList<string> Watches { get; }
        public Result<BreakTarget> BreakTarget { get; }

        public ActionEnvironment(string localWorkDir, string remoteWorkDir, IReadOnlyList<string> watches = null, Result<BreakTarget> breakTarget = null)
        {
            LocalWorkDir = localWorkDir;
            RemoteWorkDir = remoteWorkDir;
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

            await FillInitialTimestampsAsync(steps);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < steps.Count; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StepResult result;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        result = await DoCopyFileAsync(copyFile);
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
                        result = await DoReadDebugDataAsync(readDebugData);
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

        private async Task<StepResult> DoCopyFileAsync(CopyFileStep step)
        {
            IList<FileMetadata> sourceFiles;
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new ListFilesCommand
                {
                    Path = step.SourcePath,
                    WorkDir = _environment.RemoteWorkDir,
                    IncludeSubdirectories = step.IncludeSubdirectories
                };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command);
                sourceFiles = response.Files;
            }
            else
            {
                if (!TryGetMetadataForLocalPath(step.SourcePath, step.IncludeSubdirectories, out sourceFiles, out var error))
                    return new StepResult(false, error, "");
            }
            if (sourceFiles.Count == 0)
            {
                return new StepResult(false, $"Data is missing. File or directory is not found on the {(step.Direction == FileCopyDirection.RemoteToLocal ? "remote" : "local")} machine at {step.SourcePath}", "");
            }
            IList<FileMetadata> targetFiles;
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                var command = new ListFilesCommand
                {
                    Path = step.TargetPath,
                    WorkDir = _environment.RemoteWorkDir,
                    IncludeSubdirectories = step.IncludeSubdirectories
                };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command);
                targetFiles = response.Files;
            }
            else
            {
                if (!TryGetMetadataForLocalPath(step.TargetPath, step.IncludeSubdirectories, out targetFiles, out var error))
                    return new StepResult(false, error, "");
            }
            /* Copying a single file */
            if (sourceFiles.Count == 1 && !sourceFiles[0].IsDirectory)
            {
                if (targetFiles.Count > 0 && targetFiles[0].IsDirectory)
                    return new StepResult(false, $"File cannot be copied. The target path is a directory: {step.SourcePath}", "");

                if (step.IfNotModified == ActionIfNotModified.Fail && sourceFiles[0].LastWriteTimeUtc == GetInitialFileTimestamp(step.SourcePath))
                    return new StepResult(false, $"Data is stale. File was not modified on the {(step.Direction == FileCopyDirection.RemoteToLocal ? "remote" : "local")} machine at {step.SourcePath}", "");

                if (step.IfNotModified == ActionIfNotModified.DoNotCopy
                    && targetFiles.Count == 1
                    && sourceFiles[0].Size == targetFiles[0].Size
                    && sourceFiles[0].LastWriteTimeUtc == targetFiles[0].LastWriteTimeUtc)
                    return new StepResult(true, "", "No files were copied. Sizes and modification times are identical on the source and target sides.\r\n");

                return await DoCopySingleFileAsync(step);
            }
            /* Copying a directory */
            return await DoCopyDirectoryAsync(step, sourceFiles, targetFiles);
        }

        private async Task<StepResult> DoCopyDirectoryAsync(CopyFileStep step, IList<FileMetadata> sourceFiles, IList<FileMetadata> targetFiles)
        {
            /* Retrieve source files */
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
                    var command = new GetFilesCommand { RootPath = new[] { _environment.RemoteWorkDir, step.SourcePath }, Paths = filesToGet.ToArray(), UseCompression = step.UseCompression };
                    var response = await _channel.SendWithReplyAsync<GetFilesResponse>(command);

                    if (response.Status != GetFilesStatus.Successful)
                        return new StepResult(false, $"Failed to copy files from the remote machine", "The following files were requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");

                    files.AddRange(response.Files);
                }
                else
                {
                    var rootPath = Path.Combine(_environment.LocalWorkDir, step.SourcePath);
                    files.AddRange(PackedFile.PackFiles(rootPath, filesToGet));
                }
            }
            /* Include empty directories */
            foreach (var src in sourceFiles)
            {
                // ./ indicates the root directory
                if (src.IsDirectory && src.RelativePath != "./" && !(step.IfNotModified == ActionIfNotModified.DoNotCopy && SourceIdenticalToTarget(src)))
                    files.Add(new PackedFile(Array.Empty<byte>(), src.RelativePath, src.LastWriteTimeUtc));
            }
            /* Write source files and directories to the target directory */
            if (files.Count == 0)
                return new StepResult(true, "", "No files were copied. Sizes and modification times are identical on the source and target sides.\r\n");

            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                ICommand command = new PutDirectoryCommand
                {
                    Files = files.ToArray(),
                    Path = step.TargetPath,
                    WorkDir = _environment.RemoteWorkDir,
                    PreserveTimestamps = step.PreserveTimestamps
                };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutDirectoryResponse>(command);

                if (response.Status == PutDirectoryStatus.TargetPathIsFile)
                    return new StepResult(false, $"Directory cannot be copied. The target path on the remote machine is a file: {step.SourcePath}", "");
                if (response.Status == PutDirectoryStatus.PermissionDenied)
                    return new StepResult(false, $"Access is denied to remote path {step.TargetPath}", "");
                if (response.Status == PutDirectoryStatus.OtherIOError)
                    return new StepResult(false, $"Cannot copy directory to the remote machine at {step.TargetPath}", "");
            }
            else
            {
                if (!TryGetFullLocalPath(step.TargetPath, out var fullPath, out var error))
                    return new StepResult(false, error, "");
                if (File.Exists(fullPath))
                    return new StepResult(false, $"Directory cannot be copied. The target path on the local machine is a file: {step.SourcePath}", "");

                try
                {
                    PackedFile.UnpackFiles(fullPath, files, step.PreserveTimestamps);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access is denied to local file at {step.TargetPath}", "");
                }
                catch (IOException)
                {
                    return new StepResult(false, $"Cannot copy directory to the local machine at {step.TargetPath}", "");
                }
            }

            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoCopySingleFileAsync(CopyFileStep step)
        {
            byte[] sourceContents;
            /* Read source file */
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new FetchResultRange { FilePath = new[] { _environment.RemoteWorkDir, step.SourcePath } };
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(command);
                if (response.Status == FetchStatus.FileNotFound)
                    return new StepResult(false, $"Data is missing. File is not found on the remote machine at {step.SourcePath}", "");
                sourceContents = response.Data;
            }
            else
            {
                if (!ReadLocalFile(step.SourcePath, out sourceContents, out var error))
                    return new StepResult(false, error, "");
            }
            /* Write target file */
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                ICommand command = new PutFileCommand { Data = sourceContents, Path = step.TargetPath, WorkDir = _environment.RemoteWorkDir };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutFileResponse>(command);

                if (response.Status == PutFileStatus.PermissionDenied)
                    return new StepResult(false, $"Access is denied to remote file at {step.TargetPath}", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"Cannot create file on the remote machine at {step.TargetPath}", "");
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
            var workDir = step.WorkingDirectory;
            if (string.IsNullOrEmpty(workDir))
                workDir = step.Environment == StepEnvironment.Local ? _environment.LocalWorkDir : _environment.RemoteWorkDir;

            var command = new Execute
            {
                Executable = step.Executable,
                Arguments = step.Arguments,
                WorkingDirectory = workDir,
                RunAsAdministrator = step.RunAsAdmin,
                WaitForCompletion = step.WaitForCompletion,
                ExecutionTimeoutSecs = step.TimeoutSecs
            };
            ExecutionCompleted response;
            if (step.Environment == StepEnvironment.Local)
                response = await new ObservableProcess(command).StartAndObserveAsync(cancellationToken);
            else
                response = await _channel.SendWithReplyAsync<ExecutionCompleted>(command);

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
                    return new StepResult(false, $"{step.Executable} process exited with a non-zero code ({response.ExitCode}). Check your application or debug script output in Output -> RAD Debug.", log.ToString(), errorListOutput: new string[] { stdout, stderr });
                case ExecutionStatus.TimedOut:
                    return new StepResult(false, $"Execution timeout is exceeded. {step.Executable} process on the {machine} machine is terminated.", log.ToString());
                default:
                    return new StepResult(false, $"{step.Executable} process could not be started on the {machine} machine. Make sure the path to the executable is specified correctly.", log.ToString());
            }
        }

        private async Task<StepResult> DoOpenInEditorAsync(OpenInEditorStep step)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var localPath = Path.Combine(_environment.LocalWorkDir, step.Path);
            VsEditor.OpenFileInEditor(_serviceProvider, localPath, null, step.LineMarker,
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

        private async Task<StepResult> DoReadDebugDataAsync(ReadDebugDataStep step)
        {
            BreakTarget breakTarget;
            {
                if (!_environment.BreakTarget.TryGetResult(out breakTarget, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
            }
            string validWatchesString;
            {
                var result = await ReadDebugDataFileAsync("Valid watches", step.WatchesFile.Path, step.WatchesFile.IsRemote(), step.WatchesFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                validWatchesString = Encoding.UTF8.GetString(data);
            }
            string dispatchParamsString;
            {
                var result = await ReadDebugDataFileAsync("Dispatch parameters", step.DispatchParamsFile.Path, step.DispatchParamsFile.IsRemote(), step.DispatchParamsFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return new StepResult(false, error.Message, "", breakState: null);
                dispatchParamsString = Encoding.UTF8.GetString(data);
            }
            {
                var path = step.OutputFile.Path;
                var initOutputTimestamp = GetInitialFileTimestamp(path);

                BreakStateOutputFile outputFile;
                byte[] localOutputData = null;

                if (step.OutputFile.IsRemote())
                {
                    var fullPath = new[] { _environment.RemoteWorkDir, path };
                    var response = await _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = fullPath, BinaryOutput = step.BinaryOutput });

                    if (response.Status == FetchStatus.FileNotFound)
                        return new StepResult(false, $"Debug data is missing. Output file could not be found on the remote machine at {path}", "", breakState: null);
                    if (step.OutputFile.CheckTimestamp && response.Timestamp == initOutputTimestamp)
                        return new StepResult(false, $"Debug data is stale. Output file was not modified by the debug action on the remote machine at {path}", "", breakState: null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var dataDwordCount = Math.Max(0, (response.ByteCount - offset) / 4);
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dataDwordCount);
                }
                else
                {
                    var fullPath = new[] { _environment.LocalWorkDir, path };
                    var timestamp = GetLocalFileLastWriteTimeUtc(path);
                    if (step.OutputFile.CheckTimestamp && timestamp == initOutputTimestamp)
                        return new StepResult(false, $"Debug data is stale. Output file was not modified by the debug action on the local machine at {path}", "", breakState: null);

                    var readOffset = step.BinaryOutput ? step.OutputOffset : 0;
                    if (!ReadLocalFile(path, out localOutputData, out var readError, readOffset))
                        return new StepResult(false, "Debug data is missing. " + readError, "", breakState: null);
                    if (!step.BinaryOutput)
                        localOutputData = await TextDebuggerOutputParser.ReadTextOutputAsync(new MemoryStream(localOutputData), step.OutputOffset);

                    var dataDwordCount = localOutputData.Length / 4;
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, offset: 0, timestamp, dataDwordCount);
                }
                var breakStateResult = BreakState.CreateBreakState(breakTarget, _environment.Watches, validWatchesString, dispatchParamsString, outputFile, localOutputData, step.CheckMagicNumber);
                if (breakStateResult.TryGetResult(out var breakState, out var error))
                    return new StepResult(true, "", "", breakState: breakState);
                else
                    return new StepResult(false, error.Message, "", breakState: null);
            }
        }

        private async Task<Result<byte[]>> ReadDebugDataFileAsync(string type, string path, bool isRemote, bool checkTimestamp)
        {
            var initTimestamp = GetInitialFileTimestamp(path);
            if (isRemote)
            {
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                    new FetchResultRange { FilePath = new[] { _environment.RemoteWorkDir, path } });

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

        private bool ReadLocalFile(string path, out byte[] data, out string error, int byteOffset = 0)
        {
            if (!TryGetFullLocalPath(path, out var fullPath, out error))
            {
                data = null;
                return false;
            }
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
            catch (IOException e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                error = $"File is not found on the local machine at {path}";
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access is denied to local file at {path}";
            }
            data = null;
            return false;
        }

        private bool WriteLocalFile(string path, byte[] data, out string error)
        {
            try
            {
                var localPath = Path.Combine(_environment.LocalWorkDir, path);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                File.WriteAllBytes(localPath, data);
                error = "";
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access is denied to local file at {path}";
            }
            catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
            {
                error = $"Local path contains illegal characters: \"{path}\"\r\nWorking directory: \"{_environment.LocalWorkDir}\"";
            }
            return false;
        }

        private async Task FillInitialTimestampsAsync(IReadOnlyList<IActionStep> steps)
        {
            foreach (var step in steps)
            {
                if (step is CopyFileStep copyFile && copyFile.IfNotModified == ActionIfNotModified.Fail)
                {
                    if (copyFile.Direction == FileCopyDirection.RemoteToLocal)
                        _initialTimestamps[copyFile.SourcePath] = (await _channel.SendWithReplyAsync<MetadataFetched>(
                            new FetchMetadata { FilePath = new[] { _environment.RemoteWorkDir, copyFile.SourcePath } })).Timestamp;
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
                                new FetchMetadata { FilePath = new[] { _environment.RemoteWorkDir, file.Path } })).Timestamp;
                        else
                            _initialTimestamps[file.Path] = GetLocalFileLastWriteTimeUtc(file.Path);
                    }
                }
            }
        }

        private DateTime GetLocalFileLastWriteTimeUtc(string file)
        {
            try
            {
                var localPath = Path.Combine(_environment.LocalWorkDir, file);
                return File.GetLastWriteTimeUtc(localPath);
            }
            catch
            {
                return default;
            }
        }

        private bool TryGetMetadataForLocalPath(string path, bool includeSubdirectories, out IList<FileMetadata> metadata, out string error)
        {
            if (!TryGetFullLocalPath(path, out var localPath, out error))
            {
                metadata = null;
                return false;
            }

            try
            {
                metadata = FileMetadata.GetMetadataForPath(localPath, includeSubdirectories);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access to a local directory or its contents is denied at {localPath}";
                metadata = null;
                return false;
            }
            catch (IOException)
            {
                error = $"Failed to read a local directory or its contents at {localPath}";
                metadata = null;
                return false;
            }
        }

        private bool TryGetFullLocalPath(string path, out string fullPath, out string error)
        {
            try
            {
                error = "";
                fullPath = Path.Combine(_environment.LocalWorkDir, path);
                return true;
            }
            catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
            {
                error = $"Local path contains illegal characters: \"{path}\"\r\nWorking directory: \"{_environment.LocalWorkDir}\"";
                fullPath = null;
                return false;
            }
        }
    }
}
