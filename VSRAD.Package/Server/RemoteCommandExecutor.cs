using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public sealed class RemoteCommandExecutor
    {
        private const string ErrorFileNotCreated = "Output file is missing on the target machine.";
        private const string ErrorFileUnchanged = "Output file is unchanged on the target machine after running the command.";
        private static string ErrorTimedOut(string tag) =>
            "Execution timeout is exceeded. " + tag + " command on the target machine is terminated.";
        private static string ErrorCouldNotLaunch(string tag) =>
            tag + " process could not be started on the target machine. Make sure the path to the executable is specified correctly.";
        public static string ErrorNonZeroExitCode(string tag, int exitCode) =>
            tag + $" command on the target machine returned a non-zero exit code ({exitCode}). Check your application or debug script output in Output -> RAD Debug.";

        private readonly ICommunicationChannel _channel;
        private readonly IOutputWindowWriter _outputWriter;
        private readonly IErrorListManager _errorListManager;
        private readonly string _outputTag;

        public RemoteCommandExecutor(string outputTag, ICommunicationChannel channel, IOutputWindowManager outputWindow, IErrorListManager errorListManager = null)
        {
            _outputTag = outputTag;
            _channel = channel;
            _outputWriter = outputWindow.GetExecutionResultPane();
            _errorListManager = errorListManager;
        }

        public async Task<Result<(ExecutionCompleted, byte[])>> ExecuteWithResultAsync(Execute command, Options.OutputFile output, int byteCount = 0, bool checkExitCode = true)
        {
            var initialMetadata = await _channel.SendWithReplyAsync<MetadataFetched>(
                new FetchMetadata { FilePath = output.Path, BinaryOutput = output.BinaryOutput }).ConfigureAwait(false);
            var initialTimestamp = initialMetadata.Timestamp;

            var executionResult = await ExecuteAsync(command, checkExitCode).ConfigureAwait(false);
            if (!executionResult.TryGetResult(out var execution, out var error))
                return error;

            var dataResult = await _channel.SendWithReplyAsync<ResultRangeFetched>(
                new FetchResultRange { FilePath = output.Path, BinaryOutput = output.BinaryOutput, ByteCount = byteCount }).ConfigureAwait(false);
            if (dataResult.Status != FetchStatus.Successful)
                return new Error(ErrorFileNotCreated, title: "RAD " + _outputTag);
            if (dataResult.Timestamp == initialTimestamp)
                return new Error(ErrorFileUnchanged, title: "RAD " + _outputTag);

            return (execution, dataResult.Data);
        }

        public async Task<Result<ExecutionCompleted>> ExecuteAsync(Execute command, bool checkExitCode = true)
        {
            var result = await _channel.SendWithReplyAsync<ExecutionCompleted>(command).ConfigureAwait(false);

            var stdout = result.Stdout.TrimEnd('\r', '\n');
            var stderr = result.Stderr.TrimEnd('\r', '\n');

            var status = result.Status == ExecutionStatus.Completed ? $"exit code {result.ExitCode}"
                       : result.Status == ExecutionStatus.TimedOut ? "timed out"
                       : "could not launch";

            if (stdout.Length == 0 && stderr.Length == 0)
            {
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] No stdout/stderr captured ({status})").ConfigureAwait(false);
            }
            else
            {
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] Captured stdout ({status})", stdout).ConfigureAwait(false);
                await _outputWriter.PrintMessageAsync($"[{_outputTag}] Captured stderr ({status})", stderr).ConfigureAwait(false);
                if (_errorListManager != null)
                    await _errorListManager.AddToErrorListAsync(stderr).ConfigureAwait(false);
            }

            switch (result.Status)
            {
                case ExecutionStatus.Completed when result.ExitCode == 0 || !checkExitCode:
                    return result;
                case ExecutionStatus.Completed:
                    return new Error(ErrorNonZeroExitCode(_outputTag, result.ExitCode), title: "RAD " + _outputTag);
                case ExecutionStatus.TimedOut:
                    return new Error(ErrorTimedOut(_outputTag), title: "RAD " + _outputTag);
                default:
                    return new Error(ErrorCouldNotLaunch(_outputTag), title: "RAD " + _outputTag);
            }
        }
    }
}
