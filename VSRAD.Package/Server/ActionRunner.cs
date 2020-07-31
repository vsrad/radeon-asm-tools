using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
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

        public async Task<ActionRunResult> RunAsync(string actionName, IReadOnlyList<IActionStep> steps, IEnumerable<BuiltinActionFile> auxFiles, bool continueOnError)
        {
            var runStats = new ActionRunResult(actionName, steps);
            runStats.ContinueOnError = continueOnError;

            await FillInitialTimestampsAsync(steps, auxFiles);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < steps.Count; ++i)
            {
                StepResult result;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        result = await DoCopyFileAsync(copyFile, actionName);
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

        private async Task<StepResult> DoCopyFileAsync(CopyFileStep step, string actionName)
        {
            if (step.Direction == FileCopyDirection.LocalToRemote)
            {
                byte[] data;
                try
                {
                    var localPath = Path.Combine(_environment.LocalWorkDir, step.SourcePath);
                    data = File.ReadAllBytes(localPath);
                }
                catch (IOException e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    return new StepResult(false, $"File {step.SourcePath} is not found on the local machine", "");
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path {step.SourcePath} on the local machine is denied", "");
                }
                catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
                {
                    return new StepResult(false, $"The source path in copy file step of action {actionName} contains illegal characters.\n\nSource path: \"{step.SourcePath}\"\nWorking directory: \"{_environment.LocalWorkDir}\"", "");
                }
                var command = new PutFileCommand { Data = data, Path = step.TargetPath, WorkDir = _environment.RemoteWorkDir };
                var response = await _channel.SendWithReplyAsync<PutFileResponse>(command);
                if (response.Status == PutFileStatus.PermissionDenied)
                    return new StepResult(false, $"Access to path {step.TargetPath} on the remote machine is denied", "");
                if (response.Status == PutFileStatus.OtherIOError)
                    return new StepResult(false, $"File {step.TargetPath} could not be created on the remote machine", "");
            }
            else
            {
                var command = new FetchResultRange { FilePath = new[] { _environment.RemoteWorkDir, step.SourcePath } };
                var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(command);
                if (response.Status == FetchStatus.FileNotFound)
                    return new StepResult(false, $"File is not found on the remote machine at {step.SourcePath}", "");
                if (step.CheckTimestamp && GetInitialFileTimestamp(step.SourcePath) == response.Timestamp)
                    return new StepResult(false, $"File is not changed on the remote machine at {step.SourcePath}", "");

                try
                {
                    var localPath = Path.Combine(_environment.LocalWorkDir, step.TargetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                    File.WriteAllBytes(localPath, response.Data);
                }
                catch (UnauthorizedAccessException)
                {
                    return new StepResult(false, $"Access to path {step.TargetPath} on the local machine is denied", "");
                }
                catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
                {
                    return new StepResult(false, $"The target path in copy file step of action {actionName} contains illegal characters.\n\nTarget path: \"{step.TargetPath}\"\nWorking directory: \"{_environment.LocalWorkDir}\"", "");
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
            var subActionResult = await RunAsync(step.Name, step.EvaluatedSteps, Enumerable.Empty<BuiltinActionFile>(), continueOnError);
            return new StepResult(subActionResult.Successful, "", "", subActionResult);
        }

        private async Task FillInitialTimestampsAsync(IReadOnlyList<IActionStep> steps, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var remoteCommands = new List<ICommand>();

            foreach (var step in steps)
            {
                if (step is CopyFileStep copyFile && copyFile.CheckTimestamp)
                {
                    if (copyFile.Direction == FileCopyDirection.LocalToRemote)
                        _initialTimestamps[copyFile.SourcePath] = GetLocalFileTimestamp(copyFile.SourcePath);
                    else
                        remoteCommands.Add(new FetchMetadata { FilePath = new[] { _environment.RemoteWorkDir, copyFile.SourcePath } });
                }
            }

            foreach (var auxFile in auxFiles)
            {
                if (!auxFile.CheckTimestamp || string.IsNullOrEmpty(auxFile.Path))
                    continue;
                if (auxFile.IsRemote())
                    remoteCommands.Add(new FetchMetadata { FilePath = new[] { _environment.RemoteWorkDir, auxFile.Path } });
                else
                    _initialTimestamps[auxFile.Path] = GetLocalFileTimestamp(auxFile.Path);
            }

            if (remoteCommands.Count == 0)
                return;

            var remoteResponses = await _channel.SendBundleAsync(remoteCommands);
            for (int i = 0; i < remoteCommands.Count; ++i)
            {
                var path = ((FetchMetadata)remoteCommands[i]).FilePath[1];
                if (remoteResponses[i] is MetadataFetched metadata)
                    _initialTimestamps[path] = metadata.Timestamp;
            }
        }

        private DateTime GetLocalFileTimestamp(string file)
        {
            var localPath = Path.Combine(_environment.LocalWorkDir, file);
            try { return File.GetLastWriteTime(localPath); }
            catch { return default; }
        }
    }
}
