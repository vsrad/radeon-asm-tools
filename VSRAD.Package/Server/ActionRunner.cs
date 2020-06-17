using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;

namespace VSRAD.Package.Server
{
    public sealed class ActionRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();

        public ActionRunner(ICommunicationChannel channel)
        {
            _channel = channel;
        }

        public DateTime GetInitialFileTimestamp(string file) =>
            _initialTimestamps.TryGetValue(file, out var timestamp) ? timestamp : default;

        public async Task<ActionRunResult> RunAsync(IList<IActionStep> steps, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var runStats = new ActionRunResult(steps.Count);

            await FillInitialTimestampsAsync(steps, auxFiles);
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
                    default:
                        throw new NotImplementedException();
                }
                runStats.RecordStep(i, result);
                if (!result.Successful)
                    break;
            }

            runStats.FinishRun();
            return runStats;
        }

        private async Task<StepResult> DoCopyFileAsync(CopyFileStep step)
        {
            if (step.Direction == FileCopyDirection.LocalToRemote)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = new[] { step.RemotePath } });
            if (response.Status == FetchStatus.FileNotFound)
                return new StepResult(false, $"File is not found on the remote machine at {step.RemotePath}", "");
            if (step.CheckTimestamp && GetInitialFileTimestamp(step.RemotePath) == response.Timestamp)
                return new StepResult(false, $"File is not changed on the remote machine at {step.RemotePath}", "");

            File.WriteAllBytes(step.LocalPath, response.Data);
            return new StepResult(true, "", "");
        }

        private async Task<StepResult> DoExecuteAsync(ExecuteStep step)
        {
            if (step.Environment == StepEnvironment.Local)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ExecutionCompleted>(new Execute
            {
                Executable = step.Executable,
                Arguments = step.Arguments,
            });

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

            switch (response.Status)
            {
                case ExecutionStatus.Completed when response.ExitCode == 0:
                    return new StepResult(true, "", log.ToString());
                case ExecutionStatus.Completed:
                    return new StepResult(true, $"{step.Executable} process exited with a non-zero code ({response.ExitCode}). Check your application or debug script output in Output -> RAD Debug.", log.ToString());
                case ExecutionStatus.TimedOut:
                    return new StepResult(false, $"Execution timeout is exceeded. {step.Executable} process on the remote machine is terminated.", log.ToString());
                default:
                    return new StepResult(false, $"{step.Executable} process could not be started on the remote machine. Make sure the path to the executable is specified correctly.", log.ToString());
            }
        }

        private async Task FillInitialTimestampsAsync(IList<IActionStep> steps, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var remoteCommands = new List<ICommand>();

            foreach (var step in steps)
            {
                if (step is CopyFileStep copyFile && copyFile.CheckTimestamp)
                    remoteCommands.Add(new FetchMetadata { FilePath = new[] { copyFile.RemotePath } });
            }

            foreach (var auxFile in auxFiles)
            {
                if (!auxFile.CheckTimestamp)
                    continue;
                if (auxFile.Type == StepEnvironment.Remote)
                    remoteCommands.Add(new FetchMetadata { FilePath = new[] { auxFile.Path } });
                else
                    _initialTimestamps[auxFile.Path] = GetLocalFileTimestamp(auxFile.Path);
            }

            if (remoteCommands.Count == 0)
                return;

            var remoteResponses = await _channel.SendBundleAsync(remoteCommands);
            for (int i = 0; i < remoteCommands.Count; ++i)
            {
                var path = ((FetchMetadata)remoteCommands[i]).FilePath[0];
                if (remoteResponses[i] is MetadataFetched metadata)
                    _initialTimestamps[path] = metadata.Timestamp;
            }
        }

        private static DateTime GetLocalFileTimestamp(string file)
        {
            try { return File.GetLastWriteTime(file); }
            catch { return default; }
        }
    }
}
