using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

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
            var fullPath = Path.Combine(_command.DstPath, _command.Metadata.relativePath_);
            await _client.ReceiveFileAsync(fullPath);

            File.SetLastWriteTimeUtc(fullPath, _command.Metadata.lastWriteTimeUtc_);

            return new SendFileResponse { Status = SendFileStatus.Successful };
        }
    }
}
