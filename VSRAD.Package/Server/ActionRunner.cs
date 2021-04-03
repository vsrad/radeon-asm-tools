using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Server
{
    public sealed class ActionRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();
        private readonly ActionEnvironment _environment;

        public ActionRunner(ICommunicationChannel channel, SVsServiceProvider serviceProvider, ActionEnvironment environment)
        {
            _channel = channel;
            _serviceProvider = serviceProvider;
            _environment = environment;
        }

        public DateTime GetInitialFileTimestamp(string file) =>
            _initialTimestamps.TryGetValue(file, out var timestamp) ? timestamp : default;

        public async Task<ActionRunResult> RunAsync(string actionName, IReadOnlyList<IActionStep> steps, bool continueOnError = true)
        {
            var runStats = new ActionRunResult(actionName, steps, continueOnError);

            await FillInitialTimestampsAsync(steps);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < steps.Count; ++i)
            {
                StepResult result;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        result = await DoCopyFileAsync(copyFile);
                        break;
                    case ExecuteStep execute:
                        result = await DoExecuteAsync(execute);
                        break;
                    case OpenInEditorStep openInEditor:
                        result = await DoOpenInEditorAsync(openInEditor);
                        break;
                    case RunActionStep runAction:
                        result = await DoRunActionAsync(runAction, continueOnError);
                        break;
                    case ReadDebugDataStep readDebugData:
                        (result, runStats.BreakState) = await DoReadDebugDataAsync(readDebugData);
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
            IList<FileMetadata> sourceFiles, targetFiles;
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new ListFilesCommand { Path = step.SourcePath, WorkDir = _environment.RemoteWorkDir };
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
                var cwd = step.Direction == FileCopyDirection.RemoteToLocal ? _environment.RemoteWorkDir : _environment.LocalWorkDir;
                return new StepResult(false, $"Path \"{step.SourcePath}\" does not exist\r\nWorking directory: \"{cwd}\"", "");
            }
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                var command = new ListFilesCommand { Path = step.TargetPath, WorkDir = _environment.RemoteWorkDir };
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
                    return new StepResult(false, $"File \"{step.SourcePath}\" cannot be copied: the target path is a directory.", "");

                if (step.FailIfNotModified && sourceFiles[0].LastWriteTimeUtc == GetInitialFileTimestamp(step.SourcePath))
                    return new StepResult(false, "File was not changed after executing the previous steps. Disable Check Timestamp in step options to skip the modification date check.", "");

                if (step.SkipIfNotModified
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
                if (!src.IsDirectory && !(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
                    filesToGet.Add(src.RelativePath);
            }
            if (filesToGet.Count > 0)
            {
                if (step.Direction == FileCopyDirection.RemoteToLocal)
                {
                    var command = new GetFilesCommand { RootPath = new[] { _environment.RemoteWorkDir, step.SourcePath }, Paths = filesToGet.ToArray(), UseCompression = step.UseCompression };
                    var response = await _channel.SendWithReplyAsync<GetFilesResponse>(command);

                    if (response.Status != GetFilesStatus.Successful)
                        return new StepResult(false, $"Unable to copy files from the remote machine", "The following files were requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");

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
                if (src.IsDirectory && src.RelativePath != "./" && !(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
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
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the remote machine: the target path is a file.", "");
                if (response.Status == PutDirectoryStatus.PermissionDenied)
                    return new StepResult(false, $"Access to path \"{step.TargetPath}\" on the remote machine is denied", "");
                if (response.Status == PutDirectoryStatus.OtherIOError)
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the remote machine", "");
            }
            else
            {
                if (!TryGetFullLocalPath(step.TargetPath, out var fullPath, out var error))
                    return new StepResult(false, error, "");
                if (File.Exists(fullPath))
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the local machine: the target path is a file.", "");

                try
                {
                    PackedFile.UnpackFiles(fullPath, files, step.PreserveTimestamps);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path \"{step.TargetPath}\" on the local machine is denied", "");
                }
                catch (IOException)
                {
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the local machine", "");
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
                    return new StepResult(false, $"File is not found on the remote machine at {step.SourcePath}", "");
                sourceContents = response.Data;
            }
            else if (!ReadLocalFile(step.SourcePath, out sourceContents, out var error))
            {
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
                    return new StepResult(false, $"Access to path {step.TargetPath} on the remote machine is denied", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"File {step.TargetPath} could not be created on the remote machine", "");
            }
            else
            {
                if (!TryGetFullLocalPath(step.TargetPath, out var localPath, out var error))
                    return new StepResult(false, error, "");

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                    File.WriteAllBytes(localPath, sourceContents);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path {step.TargetPath} on the local machine is denied", "");
                }
            }

            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoExecuteAsync(ExecuteStep step)
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
                response = await new ObservableProcess(command).StartAndObserveAsync();
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
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            VsEditor.OpenFileInEditor(_serviceProvider, step.Path, step.LineMarker);
            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoRunActionAsync(RunActionStep step, bool continueOnError)
        {
            var subActionResult = await RunAsync(step.Name, step.EvaluatedSteps, continueOnError);
            return new StepResult(subActionResult.Successful, "", "", subActionResult);
        }

        private async Task<(StepResult, BreakState)> DoReadDebugDataAsync(ReadDebugDataStep step)
        {
            var watches = _environment.Watches;
            BreakStateDispatchParameters dispatchParams = null;

            if (!string.IsNullOrEmpty(step.WatchesFile.Path))
            {
                var result = await ReadDebugDataFileAsync("Valid watches", step.WatchesFile.Path, step.WatchesFile.IsRemote(), step.WatchesFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return (new StepResult(false, error.Message, ""), null);

                var watchString = Encoding.UTF8.GetString(data);
                var watchArray = watchString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                watches = Array.AsReadOnly(watchArray);
            }
            if (!string.IsNullOrEmpty(step.DispatchParamsFile.Path))
            {
                var result = await ReadDebugDataFileAsync("Dispatch parameters", step.DispatchParamsFile.Path, step.DispatchParamsFile.IsRemote(), step.DispatchParamsFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return (new StepResult(false, error.Message, ""), null);

                var paramsString = Encoding.UTF8.GetString(data);
                var dispatchParamsResult = BreakStateDispatchParameters.Parse(paramsString);
                if (!dispatchParamsResult.TryGetResult(out dispatchParams, out error))
                    return (new StepResult(false, error.Message, ""), null);
            }
            {
                var path = step.OutputFile.Path;
                var initOutputTimestamp = GetInitialFileTimestamp(path);

                int GetOutputDwordCount(int fileByteCount, out string warning)
                {
                    warning = "";
                    var fileDwordCount = fileByteCount / 4;
                    if (dispatchParams == null)
                        return fileDwordCount;

                    var laneDataSize = 1 /* system watch */ + watches.Count;
                    var totalLaneCount = dispatchParams.GridSizeX * dispatchParams.GridSizeY * dispatchParams.GridSizeZ;
                    var dispatchDwordCount = (int)totalLaneCount * laneDataSize;

                    if (fileDwordCount < dispatchDwordCount)
                    {
                        warning = $"Output file ({path}) is smaller than expected.\r\n\r\n" +
                            $"Grid size as specified in the dispatch parameters file is ({dispatchParams.GridSizeX}, {dispatchParams.GridSizeY}, {dispatchParams.GridSizeZ}), " +
                            $"which corresponds to {totalLaneCount} lanes. With {laneDataSize} DWORDs per lane, the output file is expected to contain at least " +
                            $"{dispatchDwordCount} DWORDs, but it only contains {fileDwordCount} DWORDs.";
                    }

                    return Math.Min(dispatchDwordCount, fileDwordCount);
                }

                BreakStateOutputFile outputFile;
                byte[] localOutputData = null;
                string stepWarning;

                if (step.OutputFile.IsRemote())
                {
                    var fullPath = new[] { _environment.RemoteWorkDir, path };
                    var response = await _channel.SendWithReplyAsync<MetadataFetched>(new FetchMetadata { FilePath = fullPath, BinaryOutput = step.BinaryOutput });

                    if (response.Status == FetchStatus.FileNotFound)
                        return (new StepResult(false, $"Output file ({path}) could not be found.", ""), null);
                    if (step.OutputFile.CheckTimestamp && response.Timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({path}) was not modified. Data may be stale.", ""), null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var dataByteCount = Math.Max(0, response.ByteCount - offset);
                    var dataDwordCount = GetOutputDwordCount(dataByteCount, out stepWarning);
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dataDwordCount);
                }
                else
                {
                    var fullPath = new[] { _environment.LocalWorkDir, path };
                    var timestamp = GetLocalFileLastWriteTimeUtc(path);
                    if (step.OutputFile.CheckTimestamp && timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({path}) was not modified. Data may be stale.", ""), null);

                    var readOffset = step.BinaryOutput ? step.OutputOffset : 0;
                    if (!ReadLocalFile(path, out localOutputData, out var readError, readOffset))
                        return (new StepResult(false, "Output file could not be opened. " + readError, ""), null);
                    if (!step.BinaryOutput)
                        localOutputData = await TextDebuggerOutputParser.ReadTextOutputAsync(new MemoryStream(localOutputData), step.OutputOffset);

                    var dataDwordCount = GetOutputDwordCount(localOutputData.Length, out stepWarning);
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, offset: 0, timestamp, dataDwordCount);
                }

                var data = new BreakStateData(watches, outputFile, localOutputData);
                return (new StepResult(true, stepWarning, ""), new BreakState(data, dispatchParams));
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
                    return new Error($"{type} file ({path}) could not be found.");
                if (checkTimestamp && response.Timestamp == initTimestamp)
                    return new Error($"{type} file ({path}) was not modified.");

                return response.Data;
            }
            else
            {
                if (checkTimestamp && GetLocalFileLastWriteTimeUtc(path) == initTimestamp)
                    return new Error($"{type} file ({path}) was not modified.");
                if (!ReadLocalFile(path, out var data, out var error))
                    return new Error($"{type} file could not be opened. {error}");
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
                error = $"File {path} is not found on the local machine";
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access to path {path} on the local machine is denied";
            }
            data = null;
            return false;
        }

        private async Task FillInitialTimestampsAsync(IReadOnlyList<IActionStep> steps)
        {
            foreach (var step in steps)
            {
                if (step is CopyFileStep copyFile && copyFile.FailIfNotModified)
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
                error = $"Access to directory or its contents is denied: \"{localPath}\"";
                metadata = null;
                return false;
            }
            catch (IOException)
            {
                error = $"Unable to open directory or its contents: \"{localPath}\"";
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
