using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public sealed class ActionRunStats
    {
        public long InitTimestampFetchMillis { get; private set; }
        public long[] ActionRunMillis { get; }
        public long TotalMillis { get; private set; }

        private readonly Stopwatch _stopwatch;
        private long _lastRecordedTime;

        public ActionRunStats(int actionCount)
        {
            ActionRunMillis = new long[actionCount];
            _stopwatch = Stopwatch.StartNew();
        }

        public void RecordInitTimestampFetch() =>
            InitTimestampFetchMillis = MeasureInterval();

        public void RecordAction(int actionIndex) =>
            ActionRunMillis[actionIndex] = MeasureInterval();

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

        public async Task<Result<ActionRunStats>> RunAsync(IList<IAction> actions, IEnumerable<BuiltinActionFile> auxFiles)
        {
            var runStats = new ActionRunStats(actions.Count);

            await FillInitialTimestampsAsync(actions, auxFiles);
            runStats.RecordInitTimestampFetch();

            for (int i = 0; i < actions.Count; ++i)
            {
                Error error = Error.Empty;
                switch (actions[i])
                {
                    case CopyFileAction copyFile:
                        error = await DoCopyFileAsync(copyFile);
                        break;
                    case ExecuteAction execute:
                        error = await DoExecuteAsync(execute);
                        break;
                }
                runStats.RecordAction(i);
                if (error != Error.Empty)
                    return new Error(error.Message, title: $"Error while executing action #{i}");
            }

            runStats.FinishRun();
            return runStats;
        }

        private async Task<Error> DoCopyFileAsync(CopyFileAction action)
        {
            if (action.Direction == FileCopyDirection.LocalToRemote)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(new FetchResultRange { FilePath = new[] { action.RemotePath } });
            if (response.Status == FetchStatus.FileNotFound)
                return new Error($"File not found at {action.RemotePath}");
            if (action.CheckTimestamp && GetInitialFileTimestamp(action.RemotePath) == response.Timestamp)
                return new Error($"File is not changed on the remote machine at {action.RemotePath}");
            File.WriteAllBytes(action.LocalPath, response.Data);

            return Error.Empty;
        }

        private async Task<Error> DoExecuteAsync(ExecuteAction action)
        {
            if (action.Environment == ActionEnvironment.Local)
                throw new NotImplementedException();

            var response = await _channel.SendWithReplyAsync<ExecutionCompleted>(new Execute
            {
                Executable = action.Executable,
                Arguments = action.Arguments,
            });

            return Error.Empty;
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
