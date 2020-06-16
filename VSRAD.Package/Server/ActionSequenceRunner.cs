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
        public long[] ActionRunMillis { get; }
        public long InitTimestampFetchMillis { get; private set; }
        public long TotalMillis { get; private set; }

        public (bool success, string log)[] ActionResults { get; }

        public bool Successful => ActionResults.All((r) => r.success);

        private readonly Stopwatch _stopwatch;
        private long _lastRecordedTime;

        public ActionRunResult(int actionCount)
        {
            ActionRunMillis = new long[actionCount];
            ActionResults = new (bool success, string log)[actionCount];
            _stopwatch = Stopwatch.StartNew();
        }

        public void RecordInitTimestampFetch() =>
            InitTimestampFetchMillis = MeasureInterval();

        public void RecordAction(int actionIndex, (bool, string) status)
        {
            ActionRunMillis[actionIndex] = MeasureInterval();
            ActionResults[actionIndex] = status;
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

    public sealed class ActionSequenceRunner
    {
        private readonly ICommunicationChannel _channel;
        private readonly Dictionary<string, DateTime> _initialTimestamps = new Dictionary<string, DateTime>();

        public ActionSequenceRunner(ICommunicationChannel channel)
        {
            _channel = channel;
        }

        public DateTime GetInitialFileTimestamp(string file) =>
            _initialTimestamps.TryGetValue(file, out var timestamp) ? timestamp : default;

        public async Task<ActionRunResult> RunAsync(IList<IAction> actions, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var runStats = new ActionRunResult(actions.Count);

            await FillInitialTimestampsAsync(actions, auxFiles);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < actions.Count; ++i)
            {
                (bool success, string log) status;
                switch (actions[i])
                {
                    case CopyFileAction copyFile:
                        status = await DoCopyFileAsync(copyFile);
                        break;
                    case ExecuteAction execute:
                        status = await DoExecuteAsync(execute);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                runStats.RecordAction(i, status);
                if (!status.success)
                    break;
            }

            runStats.FinishRun();
            return runStats;
        }

        private async Task<(bool success, string log)> DoCopyFileAsync(CopyFileAction action)
        {
            if (action.Direction == FileCopyDirection.LocalToRemote)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = new[] { action.RemotePath } });
            if (response.Status == FetchStatus.FileNotFound)
                return (false, $"File is not found on the remote machine at {action.RemotePath}");
            if (action.CheckTimestamp && GetInitialFileTimestamp(action.RemotePath) == response.Timestamp)
                return (false, $"File is not changed on the remote machine at {action.RemotePath}");
            File.WriteAllBytes(action.LocalPath, response.Data);

            return (true, $"Copied {action.RemotePath} to {action.LocalPath}");
        }

        private async Task<(bool success, string log)> DoExecuteAsync(ExecuteAction action)
        {
            if (action.Environment == ActionEnvironment.Local)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ExecutionCompleted>(new Execute
            {
                Executable = action.Executable,
                Arguments = action.Arguments,
            });

            return (true, "");
        }

        private async Task FillInitialTimestampsAsync(IList<IAction> actions, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var remoteCommands = new List<ICommand>();

            foreach (var action in actions)
            {
                if (action is CopyFileAction copyFile && copyFile.CheckTimestamp)
                    remoteCommands.Add(new FetchMetadata { FilePath = new[] { copyFile.RemotePath } });
            }

            foreach (var auxFile in auxFiles)
            {
                if (!auxFile.CheckTimestamp)
                    continue;
                if (auxFile.Type == ActionEnvironment.Remote)
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
