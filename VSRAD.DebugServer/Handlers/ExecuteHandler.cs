using System.Collections.Generic;
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

            var response = await _process.StartAndObserveAsync(ShouldTerminateProcessesOnTimeout, CancellationToken.None);

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
                await _client.SendResponseAsync(new ExecutionTimedOutResponse { ProcessTree = processTree.ToArray() });
                var command = await _client.ReadCommandAsync();
                if (command is ExecutionTimedOutActionCommand action)
                    return action.TerminateProcesses;
            }
            return true;
        }
    }
}
