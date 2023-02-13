using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutDirectoryHandler : IHandler
    {
        private readonly PutDirectoryCommand _command;

        public PutDirectoryHandler(PutDirectoryCommand command)
        {
            _command = command;
        }

        public async Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.DstPath, _command.Metadata.relativePath_);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            Directory.SetLastWriteTimeUtc(fullPath, _command.Metadata.lastWriteTimeUtc_);

            return new PutDirectoryResponse { Status = PutDirectoryStatus.Successful };
        }
    }
}

