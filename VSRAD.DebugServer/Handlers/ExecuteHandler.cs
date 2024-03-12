using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.Logging;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ExecuteHandler : IHandler
    {
        private readonly ObservableProcess _process;

        public ExecuteHandler(IPC.Commands.Execute command, ClientLogger log)
        {
            _process = new ObservableProcess(command);
            _process.ExecutionStarted += (s, e) => log.ExecutionStarted();
            _process.StdoutRead += (s, stdout) => log.StdoutReceived(stdout);
            _process.StderrRead += (s, stderr) => log.StderrReceived(stderr);
        }

        public async Task<IResponse> RunAsync() =>
            await _process.StartAndObserveAsync();
    }
}
