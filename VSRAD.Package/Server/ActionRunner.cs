using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Server
{
    public interface IActionRunController
    {
        CancellationToken CancellationToken { get; }

        Task<bool> ShouldTerminateProcessOnTimeoutAsync(IList<ProcessTreeItem> processTree);

        Task OpenFileInVsEditorAsync(string path, string lineMarker);
    }

    public sealed class ActionRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly IActionRunController _controller;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();
        private readonly ReadOnlyCollection<string> _debugWatches;

        public ActionRunner(ICommunicationChannel channel, IActionRunController controller, ReadOnlyCollection<string> debugWatches)
        {
            _channel = channel;
            _controller = controller;
            _debugWatches = debugWatches;
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
                _controller.CancellationToken.ThrowIfCancellationRequested();

                StepResult result;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        result = await DoCopyFileAsync(copyFile);
                        break;
                    case ExecuteStep execute:
                        result = await DoExecuteAsync(execute);
                        break;
                    case ReadDebugDataStep readDebugData:
                        (result, runStats.BreakState) = await DoReadDebugDataAsync(readDebugData);
                        break;
                    case OpenInEditorStep openInEditor:
                        await _controller.OpenFileInVsEditorAsync(openInEditor.Path, openInEditor.LineMarker);
                        result = new StepResult(true, "", "");
                        break;
                    case RunActionStep runAction:
                        var subActionResult = await RunAsync(runAction.Name, runAction.EvaluatedSteps, continueOnError);
                        result = new StepResult(subActionResult.Successful, "", "", subActionResult);
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
            // List all source files
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new ListFilesCommand { Path = step.SourcePath, IncludeSubdirectories = step.IncludeSubdirectories };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, _controller.CancellationToken);
                sourceFiles = response.Files;
            }
            else
            {
                if (!TryGetLocalMetadata(step.SourcePath, step.IncludeSubdirectories, out sourceFiles, out var error))
                    return new StepResult(false, error, "");
            }
            if (sourceFiles.Count == 0)
            {
                return new StepResult(false, $"Path \"{step.SourcePath}\" does not exist", "");
            }
            // List all target files
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                var command = new ListFilesCommand { Path = step.TargetPath, IncludeSubdirectories = step.IncludeSubdirectories };
                var response = await _channel.SendWithReplyAsync<ListFilesResponse>(command, _controller.CancellationToken);
                targetFiles = response.Files;
            }
            else
            {
                if (!TryGetLocalMetadata(step.TargetPath, step.IncludeSubdirectories, out targetFiles, out var error))
                    return new StepResult(false, error, "");
            }
            // Copying one file?
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
            // Copying a directory?
            return await DoCopyDirectoryAsync(step, sourceFiles, targetFiles);
        }

        private async Task<StepResult> DoCopyDirectoryAsync(CopyFileStep step, IList<FileMetadata> sourceFiles, IList<FileMetadata> targetFiles)
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
                if (!src.IsDirectory && !(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
                    filesToGet.Add(src.RelativePath);
            }
            if (filesToGet.Count > 0)
            {
                if (step.Direction == FileCopyDirection.RemoteToLocal)
                {
                    var command = new GetFilesCommand { RootPath = step.SourcePath, Paths = filesToGet.ToArray(), UseCompression = step.UseCompression };
                    var response = await _channel.SendWithReplyAsync<GetFilesResponse>(command, _controller.CancellationToken);

                    if (response.Status != GetFilesStatus.Successful)
                        return new StepResult(false, $"Unable to copy files from the remote machine", "The following files were requested:\r\n" + string.Join("; ", filesToGet) + "\r\n");

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
                if (src.IsDirectory && src.RelativePath != "./" && !(step.SkipIfNotModified && SourceIdenticalToTarget(src)))
                    files.Add(new PackedFile(Array.Empty<byte>(), src.RelativePath, src.LastWriteTimeUtc));
            }
            // Write source files and directories to the target directory
            if (files.Count == 0)
            {
                return new StepResult(true, "", "No files were copied. Sizes and modification times are identical on the source and target sides.\r\n");
            }
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                ICommand command = new PutDirectoryCommand { Files = files.ToArray(), Path = step.TargetPath, PreserveTimestamps = step.PreserveTimestamps };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutDirectoryResponse>(command, _controller.CancellationToken);

                if (response.Status == PutDirectoryStatus.TargetPathIsFile)
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the remote machine: the target path is a file.", "");
                if (response.Status == PutDirectoryStatus.PermissionDenied)
                    return new StepResult(false, $"Access to path \"{step.TargetPath}\" on the remote machine is denied", "");
                if (response.Status == PutDirectoryStatus.OtherIOError)
                    return new StepResult(false, $"Directory \"{step.SourcePath}\" could not be copied to the remote machine", "");
            }
            else
            {
                if (File.Exists(step.TargetPath))
                    return new StepResult(false, $"Directory \"{step.TargetPath}\" could not be copied to the local machine: the target path is a file.", "");

                try
                {
                    PackedFile.UnpackFiles(step.TargetPath, files, step.PreserveTimestamps);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path \"{step.TargetPath}\" on the local machine is denied", "");
                }
                catch (IOException)
                {
                    return new StepResult(false, $"Directory \"{step.TargetPath}\" could not be copied to the local machine", "");
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
                var command = new FetchResultRange { FilePath = new[] { step.SourcePath } };
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(command, _controller.CancellationToken);
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
                ICommand command = new PutFileCommand { Data = sourceContents, Path = step.TargetPath };
                if (step.UseCompression)
                    command = new CompressedCommand(command);
                var response = await _channel.SendWithReplyAsync<PutFileResponse>(command, _controller.CancellationToken);

                if (response.Status == PutFileStatus.PermissionDenied)
                    return new StepResult(false, $"Access to path {step.TargetPath} on the remote machine is denied", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"File {step.TargetPath} could not be created on the remote machine", "");
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(step.TargetPath));
                    File.WriteAllBytes(step.TargetPath, sourceContents);
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
            var command = new Execute
            {
                Executable = step.Executable,
                Arguments = step.Arguments,
                WorkingDirectory = step.WorkingDirectory,
                RunAsAdministrator = step.RunAsAdmin,
                WaitForCompletion = step.WaitForCompletion,
                ExecutionTimeoutSecs = step.TimeoutSecs
            };
            IResponse response;
            if (step.Environment == StepEnvironment.Local)
            {
                response = await new ObservableProcess(command).StartAndObserveAsync(tree =>
                    step.ConfirmTerminationOnTimeout ? _controller.ShouldTerminateProcessOnTimeoutAsync(tree) : Task.FromResult(true), _controller.CancellationToken);
            }
            else
            {
                response = await _channel.SendWithReplyAsync<IResponse>(command, _controller.CancellationToken);
                if (response is ExecutionTimedOutResponse timeoutResponse)
                {
                    var shouldTerminate = !step.ConfirmTerminationOnTimeout || await _controller.ShouldTerminateProcessOnTimeoutAsync(timeoutResponse.ProcessTree);
                    response = await _channel.SendWithReplyAsync<IResponse>(
                        new ExecutionTimedOutActionCommand { TerminateProcesses = shouldTerminate }, _controller.CancellationToken);
                }
            }

            var machine = step.Environment == StepEnvironment.Local ? "local" : "remote";
            if (response is ExecutionTerminatedResponse terminatedResponse)
            {
                var log = new StringBuilder("The following processes were terminated:\r\n");
                ProcessUtils.PrintProcessTree(log, terminatedResponse.TerminatedProcessTree);

                return new StepResult(false, $"Execution timeout is exceeded. {step.Executable} process on the {machine} machine is terminated.", log.ToString());
            }
            else
            {
                var result = (ExecutionCompleted)response;
                if (result.Status == ExecutionStatus.Completed)
                {
                    var log = new StringBuilder();
                    var stdout = result.Stdout.TrimEnd('\r', '\n');
                    var stderr = result.Stderr.TrimEnd('\r', '\n');
                    if (stdout.Length == 0 && stderr.Length == 0)
                        log.AppendFormat("No stdout/stderr captured (exit code {0})\r\n", result.ExitCode);
                    if (stdout.Length != 0)
                        log.AppendFormat("Captured stdout (exit code {0}):\r\n{1}\r\n", result.ExitCode, stdout);
                    if (stderr.Length != 0)
                        log.AppendFormat("Captured stderr (exit code {0}):\r\n{1}\r\n", result.ExitCode, stderr);

                    if (result.ExitCode == 0)
                        return new StepResult(true, "", log.ToString(), errorListOutput: new string[] { stdout, stderr });
                    else
                        return new StepResult(false, $"{step.Executable} process exited with a non-zero code ({result.ExitCode}). Check your application or debug script output in Output -> RAD Debug.", log.ToString(), errorListOutput: new string[] { stdout, stderr });
                }
                else
                {
                    // result.Stderr contains the error reason 
                    return new StepResult(false, $"{step.Executable} process could not be started on the {machine} machine. {result.Stderr}", result.Stderr + "\r\n");
                }
            }
        }

        private async Task<(StepResult, BreakState)> DoReadDebugDataAsync(ReadDebugDataStep step)
        {
            var watches = _debugWatches;
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
                var outputPath = step.OutputFile.Path;
                var initOutputTimestamp = GetInitialFileTimestamp(outputPath);

                int GetDispatchDwordCount(int fileDwordCount, out string warning)
                {
                    warning = "";
                    if (dispatchParams == null)
                        return fileDwordCount;

                    var laneDataSize = 1 /* system watch */ + watches.Count;
                    var totalLaneCount = dispatchParams.GridSizeX * dispatchParams.GridSizeY * dispatchParams.GridSizeZ;
                    var dispatchDwordCount = (int)totalLaneCount * laneDataSize;

                    if (fileDwordCount < dispatchDwordCount)
                    {
                        warning = $"Output file ({outputPath}) is smaller than expected.\r\n\r\n" +
                            $"Grid size as specified in the dispatch parameters file is ({dispatchParams.GridSizeX}, {dispatchParams.GridSizeY}, {dispatchParams.GridSizeZ}), " +
                            $"which corresponds to {totalLaneCount} lanes. With {laneDataSize} DWORDs per lane, the output file is expected to contain at least " +
                            $"{dispatchDwordCount} DWORDs, but it only contains {fileDwordCount} DWORDs.";
                    }

                    return Math.Min(dispatchDwordCount, fileDwordCount);
                }

                BreakStateOutputFile outputFile;
                uint[] localOutputData = null;
                string stepWarning;

                if (step.OutputFile.IsRemote())
                {
                    var response = await _channel.SendWithReplyAsync<MetadataFetched>(
                        new FetchMetadata { FilePath = new[] { outputPath }, BinaryOutput = step.BinaryOutput }, _controller.CancellationToken);

                    if (response.Status == FetchStatus.FileNotFound)
                        return (new StepResult(false, $"Output file ({outputPath}) could not be found.", ""), null);
                    if (step.OutputFile.CheckTimestamp && response.Timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({outputPath}) was not modified. Data may be stale.", ""), null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var fileByteCount = Math.Max(0, response.ByteCount - offset);
                    var dispatchDwordCount = GetDispatchDwordCount(fileDwordCount: fileByteCount / 4, out stepWarning);
                    outputFile = new BreakStateOutputFile(outputPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dispatchDwordCount);
                }
                else
                {
                    var timestamp = GetLocalFileLastWriteTimeUtc(outputPath);
                    if (step.OutputFile.CheckTimestamp && timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({outputPath}) was not modified. Data may be stale.", ""), null);

                    int dispatchDwordCount;
                    if (step.BinaryOutput)
                    {
                        if (!ReadLocalFile(outputPath, out var outputBytes, out var readError))
                            return (new StepResult(false, "Output file could not be opened. " + readError, ""), null);

                        var fileByteCount = Math.Max(0, outputBytes.Length - step.OutputOffset);
                        dispatchDwordCount = GetDispatchDwordCount(fileDwordCount: fileByteCount / 4, out stepWarning);
                        localOutputData = new uint[dispatchDwordCount];
                        Buffer.BlockCopy(outputBytes, step.OutputOffset, localOutputData, 0, dispatchDwordCount * 4);
                    }
                    else
                    {
                        var outputDwords = TextDebuggerOutputParser.ReadTextOutput(outputPath, step.OutputOffset);

                        dispatchDwordCount = GetDispatchDwordCount(fileDwordCount: outputDwords.Count, out stepWarning);
                        if (outputDwords.Count > dispatchDwordCount)
                            outputDwords.RemoveRange(dispatchDwordCount, outputDwords.Count - dispatchDwordCount);
                        localOutputData = outputDwords.ToArray();
                    }

                    outputFile = new BreakStateOutputFile(outputPath, step.BinaryOutput, offset: 0, timestamp, dispatchDwordCount);
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
                    new FetchResultRange { FilePath = new[] { path } }, _controller.CancellationToken);

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

        private static bool ReadLocalFile(string fullPath, out byte[] data, out string error)
        {
            try
            {
                data = File.ReadAllBytes(fullPath);
                error = "";
                return true;
            }
            catch (IOException e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                error = $"File {fullPath} is not found on the local machine";
            }
            catch (UnauthorizedAccessException)
            {
                error = $"Access to path {fullPath} on the local machine is denied";
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
                            new FetchMetadata { FilePath = new[] { copyFile.SourcePath } }, _controller.CancellationToken)).Timestamp;
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
                                new FetchMetadata { FilePath = new[] { file.Path } }, _controller.CancellationToken)).Timestamp;
                        else
                            _initialTimestamps[file.Path] = GetLocalFileLastWriteTimeUtc(file.Path);
                    }
                }
            }
        }

        private static DateTime GetLocalFileLastWriteTimeUtc(string path)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return default;
            }
        }

        private static bool TryGetLocalMetadata(string localPath, bool includeSubdirectories, out IList<FileMetadata> metadata, out string error)
        {
            try
            {
                error = "";
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
    }
}
