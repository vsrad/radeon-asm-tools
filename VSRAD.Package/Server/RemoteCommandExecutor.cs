using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public sealed class RemoteCommandExecutor
    {
        public const string ErrorTimedOut =
            "Execution timeout has been exceeded. The process is terminated.";
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

        public Task<Result<byte[]>> ExecuteWithResultAsync(Execute command, string[] filepath) =>
            ExecuteWithResultAsync(command, filepath, true, 0, 0);

        public async Task<Result<byte[]>> ExecuteWithResultAsync(Execute command, string[] filepath, bool binaryOutput, int byteCount, int byteOffset)
        {
            var initialMetadata = await _channel.SendWithReplyAsync<MetadataFetched>(
                new FetchMetadata { FilePath = filepath, BinaryOutput = binaryOutput }).ConfigureAwait(false);
            var initialTimestamp = initialMetadata.Timestamp;

            var executionResult = await ExecuteAsync(command).ConfigureAwait(false);
            if (executionResult.TryGetResult(out _, out var error))
                return await FetchResultAsync(filepath, binaryOutput, byteCount, byteOffset, initialTimestamp);
            else
                return error;
        }

        public async Task<Result<bool>> ExecuteAsync(Execute command)
        {
            var result = await _channel.SendWithReplyAsync<ExecutionCompleted>(command).ConfigureAwait(false);

            var stdout = result.Stdout.TrimEnd('\r', '\n');
            var stderr = result.Stderr.TrimEnd('\r', '\n');

            if (stdout.Length == 0 && stderr.Length == 0)
            {
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] No stdout/stderr captured").ConfigureAwait(false);
            }
            else
            {
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] Captured stdout", stdout).ConfigureAwait(false);
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] Captured stderr", stderr).ConfigureAwait(false);
            }

            switch (result.Status)
            {
                case ExecutionStatus.Completed when result.ExitCode == 0:
                    return true;
                case ExecutionStatus.Completed:
                    return new Error(ErrorNonZeroExitCode(result.ExitCode), title: "RAD " + _outputTag);
                case ExecutionStatus.TimedOut:
                    return new Error(ErrorTimedOut, title: "RAD " + _outputTag);
                default:
                    return new Error(ErrorCouldNotLaunch, title: "RAD " + _outputTag);
            }
        }

        private async Task<Result<byte[]>> FetchResultAsync(string[] filepath, bool binaryOutput, int byteCount, int byteOffset, DateTime initialTimestamp)
        {
            var response = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                new FetchResultRange { FilePath = filepath, BinaryOutput = binaryOutput, ByteCount = byteCount, ByteOffset = byteOffset }).ConfigureAwait(false);
            if (response.Status != FetchStatus.Successful)
                return new Error(ErrorFileNotCreated);
            if (response.Timestamp == initialTimestamp)
                return new Error(ErrorFileHasNotChanged);
            return response.Data;
        }
    }
}
