using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutFilesHandler : IHandler
    {
        private readonly PutFilesCommand _command;

        public PutFilesHandler(PutFilesCommand command)
        {
            _command = command;
        }

        public async Task<IResponse> RunAsync() =>
            await FileTransfer.PutFilesAsync(_command);
    }
}
