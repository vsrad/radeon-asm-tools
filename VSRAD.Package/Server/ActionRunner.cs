using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
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
            byte[] sourceContents;
            /* Read source file */
            var initTimestamp = GetInitialFileTimestamp(step.SourcePath);
            if (step.Direction == FileCopyDirection.RemoteToLocal)
            {
                var command = new FetchResultRange { FilePath = new[] { _environment.RemoteWorkDir, step.SourcePath } };
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(command);
                if (response.Status == FetchStatus.FileNotFound)
                    return new StepResult(false, $"File is not found on the remote machine at {step.SourcePath}", "");
                if (step.CheckTimestamp && initTimestamp == response.Timestamp)
                    return new StepResult(false, $"File is not changed on the remote machine at {step.SourcePath}. Disable Check Timestamp in step options to skip the modification date check", "");
                sourceContents = response.Data;
            }
            else
            {
                if (!ReadLocalFile(step.SourcePath, out sourceContents, out var error))
                    return new StepResult(false, error, "");
                if (step.CheckTimestamp && initTimestamp == GetLocalFileTimestamp(step.SourcePath))
                    return new StepResult(false, $"File is not changed at {step.SourcePath}. Disable Check Timestamp in step options to skip the modification date check", "");
            }
            /* Write target file */
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                var command = new PutFileCommand { Data = sourceContents, Path = step.TargetPath, WorkDir = _environment.RemoteWorkDir };
                var response = await _channel.SendWithReplyAsync<PutFileResponse>(command);
                if (response.Status == PutFileStatus.PermissionDenied)
                    return new StepResult(false, $"Access to path {step.TargetPath} on the remote machine is denied", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"File {step.TargetPath} could not be created on the remote machine", "");
            }
            else
            {
                try
                {
                    var localPath = Path.Combine(_environment.LocalWorkDir, step.TargetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                    File.WriteAllBytes(localPath, sourceContents);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path {step.TargetPath} on the local machine is denied", "");
                }
                catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
                {
                    return new StepResult(false, $"Local path contains illegal characters: \"{step.TargetPath}\"\r\nWorking directory: \"{_environment.LocalWorkDir}\"", "");
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
            VsEditor.OpenFileInEditor(_serviceProvider, step.Path, step.LineMarker,
                _project.Options.DebuggerOptions.ForceOppositeTab, _project.Options.DebuggerOptions.PreserveActiveDoc);
            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoRunActionAsync(RunActionStep step, bool continueOnError)
        {
            var subActionResult = await RunAsync(step.Name, step.EvaluatedSteps, continueOnError);
            return new StepResult(subActionResult.Successful, "", "", subActionResult);
        }

        private async Task<(StepResult, BreakState)> DoReadDebugDataAsync(ReadDebugDataStep step)
        {
            string validWatchesString;
            {
                var result = await ReadDebugDataFileAsync("Valid watches", step.WatchesFile.Path, step.WatchesFile.IsRemote(), step.WatchesFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return (new StepResult(false, error.Message, ""), null);
                validWatchesString = Encoding.UTF8.GetString(data);
            }
            string dispatchParamsString = null;
            if (!string.IsNullOrEmpty(step.DispatchParamsFile.Path))
            {
                var result = await ReadDebugDataFileAsync("Dispatch parameters", step.DispatchParamsFile.Path, step.DispatchParamsFile.IsRemote(), step.DispatchParamsFile.CheckTimestamp);
                if (!result.TryGetResult(out var data, out var error))
                    return (new StepResult(false, error.Message, ""), null);
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
                        return (new StepResult(false, $"Output file ({path}) could not be found.", ""), null);
                    if (step.OutputFile.CheckTimestamp && response.Timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({path}) was not modified. Data may be stale.", ""), null);

                    var offset = step.BinaryOutput ? step.OutputOffset : step.OutputOffset * 4;
                    var dataDwordCount = Math.Max(0, (response.ByteCount - offset) / 4);
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, step.OutputOffset, response.Timestamp, dataDwordCount);
                }
                else
                {
                    var fullPath = new[] { _environment.LocalWorkDir, path };
                    var timestamp = GetLocalFileTimestamp(path);
                    if (step.OutputFile.CheckTimestamp && timestamp == initOutputTimestamp)
                        return (new StepResult(false, $"Output file ({path}) was not modified. Data may be stale.", ""), null);

                    var readOffset = step.BinaryOutput ? step.OutputOffset : 0;
                    if (!ReadLocalFile(path, out localOutputData, out var readError, readOffset))
                        return (new StepResult(false, "Output file could not be opened. " + readError, ""), null);
                    if (!step.BinaryOutput)
                        localOutputData = await TextDebuggerOutputParser.ReadTextOutputAsync(new MemoryStream(localOutputData), step.OutputOffset);

                    var dataDwordCount = localOutputData.Length / 4;
                    outputFile = new BreakStateOutputFile(fullPath, step.BinaryOutput, offset: 0, timestamp, dataDwordCount);
                }

                if (BreakState.CreateBreakState(validWatchesString, dispatchParamsString, outputFile, localOutputData).TryGetResult(out var breakState, out var error))
                    return (new StepResult(true, "", ""), breakState);
                else
                    return (new StepResult(false, error.Message, ""), null);
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
                if (checkTimestamp && GetLocalFileTimestamp(path) == initTimestamp)
                    return new Error($"{type} file ({path}) was not modified.");
                if (!ReadLocalFile(path, out var data, out var error))
                    return new Error($"{type} file could not be opened. {error}");
                return data;
            }
        }

        private bool ReadLocalFile(string path, out byte[] data, out string error, int byteOffset = 0)
        {
            try
            {
                var localPath = Path.Combine(_environment.LocalWorkDir, path);
                using (var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
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
            catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
            {
                error = $"Local path contains illegal characters: \"{path}\"\r\nWorking directory: \"{_environment.LocalWorkDir}\"";
            }
            data = null;
            return false;
        }

        private async Task FillInitialTimestampsAsync(IReadOnlyList<IActionStep> steps)
        {
            foreach (var step in steps)
            {
                if (step is CopyFileStep copyFile && copyFile.CheckTimestamp)
                {
                    if (copyFile.Direction == FileCopyDirection.RemoteToLocal)
                        _initialTimestamps[copyFile.SourcePath] = (await _channel.SendWithReplyAsync<MetadataFetched>(
                            new FetchMetadata { FilePath = new[] { _environment.RemoteWorkDir, copyFile.SourcePath } })).Timestamp;
                    else
                        _initialTimestamps[copyFile.SourcePath] = GetLocalFileTimestamp(copyFile.SourcePath);
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
                            _initialTimestamps[file.Path] = GetLocalFileTimestamp(file.Path);
                    }
                }
            }
        }

        private DateTime GetLocalFileTimestamp(string file)
        {
            try
            {
                var localPath = Path.Combine(_environment.LocalWorkDir, file);
                return File.GetLastWriteTime(localPath);
            }
            catch
            {
                return default;
            }
        }
    }
}
