using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using System.Runtime.InteropServices;


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
            var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                             ? _command.Metadata.RelativePath.Replace('\\', '/')
                             : _command.Metadata.RelativePath;

            var fullPath = Path.Combine(_command.RemoteWorkDir, _command.TargetPath, relativePath);
            try
            {
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                Directory.SetLastWriteTimeUtc(fullPath, _command.Metadata.LastWriteTimeUtc);
            } catch(Exception e)
            {
                return new PutDirectoryResponse { Status = PutDirectoryStatus.OtherIOError, Message = e.Message };
            }
            return new PutDirectoryResponse { Status = PutDirectoryStatus.Successful, Message = "" };
        }
    }
}

