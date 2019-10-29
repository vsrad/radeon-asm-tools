using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Server
{
    public sealed class RemoteCommandExecutor
    {
        public sealed class ExecutionFailedException : Exception
        {
            public ExecutionFailedException(string message) : base(message) { }
        }

        public const string ErrorTimedOut =
            "Timeout has been exceeded. The process is terminated.";
        public const string ErrorFileHasNotChanged =
            "Output file has not changed as a result of running the command.";
        public const string ErrorFileNotCreated =
            "Output file could not be found after executing the command.";
        public const string ErrorCouldNotLaunch =
            "The process could not be started. Make sure the path to the executable is specified correctly.";
        public static string ErrorNonZeroExitCode(int exitCode) =>
            $"The process has exited with a non-zero code ({exitCode}).";

        private readonly ICommunicationChannel _channel;
        private readonly IOutputWindowWriter _outputWriter;
        private readonly string _outputTag;

        public RemoteCommandExecutor(string outputTag, ICommunicationChannel channel, IOutputWindowManager outputWindow)
        {
            _outputTag = outputTag;
            _channel = channel;
            _outputWriter = outputWindow.GetExecutionResultPane();
        }

        public async Task ExecuteRemoteAsync(Execute command)
        {
            var response = await _channel.SendWithReplyAsync<ExecutionCompleted>(command).ConfigureAwait(false);
            await _outputWriter.PrintExecutionResultAsync(_outputTag, response).ConfigureAwait(false);
            if (response.Status == ExecutionStatus.Completed && response.ExitCode == 0) return;
            switch (response.Status)
            {
                case ExecutionStatus.Completed:
                    throw new ExecutionFailedException(ErrorNonZeroExitCode(response.ExitCode));
                case ExecutionStatus.TimedOut:
                    throw new ExecutionFailedException(ErrorTimedOut);
                case ExecutionStatus.CouldNotLaunch:
                    throw new ExecutionFailedException(ErrorCouldNotLaunch);
            }
        }

        public async Task<(DateTime timestamp, int byteCount)?> FetchMetadataAsync(string[] filePath, bool bin)
        {
            var response = await _channel.SendWithReplyAsync<MetadataFetched>(
                new FetchMetadata
                {
                    BinaryOutput = bin,
                    FilePath = filePath
                })
                .ConfigureAwait(false);

            if (response.Status != FetchStatus.Successful) return null;

            return (response.Timestamp, response.ByteCount);
        }

        public async Task<(DateTime, byte[])> FetchResultAsync(string[] filepath, bool binaryOutput, int byteCount, int byteOffset = 0)
        {
            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = filepath,
                    BinaryOutput = binaryOutput,
                    ByteCount = byteCount,
                    ByteOffset = byteOffset
                })
                .ConfigureAwait(false);

            if (response.Status != FetchStatus.Successful)
                throw new ExecutionFailedException(ErrorFileNotCreated);

            return (response.Timestamp, response.Data);
        }

        public Task<byte[]> ExecuteAndFetchFileWithTimestampCheckingAsync(Execute command, string[] filepath) =>
            ExecuteAndFetchResultWithTimestampCheckingAsync(command, filepath, true, 0, 0);

        public async Task<byte[]> ExecuteAndFetchResultWithTimestampCheckingAsync(Execute command, string[] filepath, bool binaryOutput, int byteCount, int byteOffset)
        {
            var initialMetadata = await FetchMetadataAsync(filepath, binaryOutput).ConfigureAwait(false);
            await ExecuteRemoteAsync(command).ConfigureAwait(false);
            var (newTimestamp, data) = await FetchResultAsync(filepath, binaryOutput, byteCount, byteOffset).ConfigureAwait(false);

            if (initialMetadata?.timestamp == newTimestamp)
                throw new ExecutionFailedException(ErrorFileHasNotChanged);

            return data;
        }
    }
}
