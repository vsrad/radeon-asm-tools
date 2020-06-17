using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;

namespace VSRAD.Package.Server
{
    public sealed class ActionRunResult
    {
        public long[] StepRunMillis { get; }
        public long InitTimestampFetchMillis { get; private set; }
        public long TotalMillis { get; private set; }

        public (bool success, string log)[] StepResults { get; }

        public bool Successful => StepResults.All((r) => r.success);

        private readonly Stopwatch _stopwatch;
        private long _lastRecordedTime;

        public ActionRunResult(int stepCount)
        {
            StepRunMillis = new long[stepCount];
            StepResults = new (bool success, string log)[stepCount];
            _stopwatch = Stopwatch.StartNew();
        }

        public void RecordInitTimestampFetch() =>
            InitTimestampFetchMillis = MeasureInterval();

        public void RecordStep(int stepIndex, (bool, string) status)
        {
            StepRunMillis[stepIndex] = MeasureInterval();
            StepResults[stepIndex] = status;
        }

        public void FinishRun() =>
            TotalMillis = _stopwatch.ElapsedMilliseconds;

        private long MeasureInterval()
        {
            var currentTime = _stopwatch.ElapsedMilliseconds;
            var elapsed = currentTime - _lastRecordedTime;
            _lastRecordedTime = currentTime;
            return elapsed;
        }
    }

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
                (bool success, string log) status;
                switch (steps[i])
                {
                    case CopyFileStep copyFile:
                        status = await DoCopyFileAsync(copyFile);
                        break;
                    case ExecuteStep execute:
                        status = await DoExecuteAsync(execute);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                runStats.RecordStep(i, status);
                if (!status.success)
                    break;
            }

            runStats.FinishRun();
            return runStats;
        }

        private async Task<(bool success, string log)> DoCopyFileAsync(CopyFileStep step)
        {
            if (step.Direction == FileCopyDirection.LocalToRemote)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = new[] { step.RemotePath } });
            if (response.Status == FetchStatus.FileNotFound)
                return (false, $"File is not found on the remote machine at {step.RemotePath}");
            if (step.CheckTimestamp && GetInitialFileTimestamp(step.RemotePath) == response.Timestamp)
                return (false, $"File is not changed on the remote machine at {step.RemotePath}");
            File.WriteAllBytes(step.LocalPath, response.Data);

            return (true, $"Copied {step.RemotePath} to {step.LocalPath}");
        }

        private async Task<(bool success, string log)> DoExecuteAsync(ExecuteStep step)
        {
            if (step.Environment == StepEnvironment.Local)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ExecutionCompleted>(new Execute
            {
                Executable = step.Executable,
                Arguments = step.Arguments,
            });

            return (true, "");
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
