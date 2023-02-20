using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
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
            var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                             ? _command.Metadata.RelativePath.Replace('\\', '/')
                             : _command.Metadata.RelativePath;

            var fullPath = Path.Combine(_command.RemoteWorkDir, _command.SrcPath, relativePath);
            try
            {
                await _client.SendFileAsync(fullPath, _command.UseCompression);
            } catch(System.Security.SecurityException ex)
            {
                return new GetFileResponse { Status = GetFileStatus.PermissionDenied };
            } catch (Exception ex)
            {
                return new GetFileResponse { Status = GetFileStatus.OtherIOError };
            }
                
            return new GetFileResponse { Status = GetFileStatus.Successful };         
        }
    }
}
