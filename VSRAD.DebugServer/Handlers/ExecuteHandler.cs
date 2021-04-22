using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ExecuteHandler : IHandler
    {
        private readonly ObservableProcess _process;
        private readonly Client _client;
        private readonly bool _clientSupportsTimeoutAction;

        public ExecuteHandler(Execute command, Client client)
        {
            _process = new ObservableProcess(command);
            _client = client;
            _clientSupportsTimeoutAction = _client.Capabilities.Contains(IPC.ExtensionCapability.ExecutionTimedOutResponse);
        }

        private void LogStdout(object sender, string stdout) => _client.Log.StdoutReceived(stdout);

        private void LogStderr(object sender, string stderr) => _client.Log.StderrReceived(stderr);

        public async Task<IResponse> RunAsync()
        {
            _process.ExecutionStarted += (s, e) => _client.Log.ExecutionStarted();
            _process.StdoutRead += LogStdout;
            _process.StderrRead += LogStderr;

            var processCts = new CancellationTokenSource();
            var processExitedTask = _process.StartAndObserveAsync(ShouldTerminateProcessesOnTimeout, processCts.Token);

            // Send a ping each second to detect if the client disconnects and terminate the process accordingly.
            // This is especially important due to the global command execution lock:
            // if the process hangs and the client disconnects, the server will never respond to another client.
            while (true)
            {
                var task = await Task.WhenAny(processExitedTask, Task.Delay(1000));
                if (task == processExitedTask)
                    break;

                try
                {
                    await _client.PingAsync();
                }
                catch (EndOfStreamException)
                {
                    processCts.Cancel();
                }
            }

            var response = await processExitedTask;

            _process.StdoutRead -= LogStdout;
            _process.StderrRead -= LogStderr;

            if (!_clientSupportsTimeoutAction && response is ExecutionTerminatedResponse)
                return new ExecutionCompleted { Status = ExecutionStatus.TimedOut };

            return response;
        }

        private async Task<bool> ShouldTerminateProcessesOnTimeout(IList<ProcessTreeItem> processTree)
        {
            if (_clientSupportsTimeoutAction)
            {
                var action = await _client.RespondWithFollowUpAsync<ExecutionTimedOutActionCommand>(
                    new ExecutionTimedOutResponse { ProcessTree = processTree.ToArray() });
                return action.TerminateProcesses;
            }
            return true;
        }
    }
}
