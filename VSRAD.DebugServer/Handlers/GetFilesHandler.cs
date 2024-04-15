using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class GetFilesHandler : IHandler
    {
        private readonly GetFilesCommand _command;

        public GetFilesHandler(GetFilesCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            IResponse response = FileTransfer.GetFiles(_command);
            if (_command.UseCompression)
                response = new CompressedResponse(response);

            return Task.FromResult(response);
        }
    }
}
