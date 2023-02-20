using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    class GetFileHandler : IHandler
    {
        private GetFileCommand _command;
        private NetworkClient _client;
        private ClientLogger _log;
        public GetFileHandler(GetFileCommand command, NetworkClient client, ClientLogger clientLog)
        {
            _command = command;
            _client = client;
            _log = clientLog;
        }

        public async Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.SrcPath, _command.Metadata.relativePath_);
            
            await _client.SendFileAsync(fullPath, _command.UseCompression);
                
            return new GetFileResponse { Status = GetFileStatus.Successful };         
        }
    }
}
