using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using System.Runtime.InteropServices;


namespace VSRAD.DebugServer.Handlers
{
    public sealed class SendFileHandler : IHandler
    {
        private readonly SendFileCommand _command;

        private NetworkClient _client;

        private static readonly TimeSpan _connectionTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        public SendFileHandler(SendFileCommand command, NetworkClient client)
        {
            _command = command;
            _client = client;
        }

        public async Task<IResponse> RunAsync()
        {
            var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                             ? _command.Metadata.RelativePath.Replace('\\', '/')
                             : _command.Metadata.RelativePath;

            var fullPath = Path.Combine(_command.RemoteWorkDir, _command.DstPath, relativePath);
            try
            {
                await _client.ReceiveFileAsync(fullPath, _command.UseCompression);
                File.SetLastWriteTimeUtc(fullPath, _command.Metadata.LastWriteTimeUtc);
            } catch (Exception e)
            {
                return new SendFileResponse { Status = SendFileStatus.OtherIOError, Message = e.Message };
            }
            return new SendFileResponse { Status = SendFileStatus.Successful, Message = "" };
        }
    }
}
