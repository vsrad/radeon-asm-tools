using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutDirectoryHandler : IHandler
    {
        private readonly PutDirectoryCommand _command;

        public PutDirectoryHandler(PutDirectoryCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.WorkDir, _command.Path);

            if (File.Exists(fullPath))
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.TargetPathIsFile });

            try
            {
                ZipUtils.UnpackToDirectory(fullPath, _command.ZipData, _command.PreserveTimestamps);
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.Successful });
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.PermissionDenied });
            }
            catch (IOException)
            {
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.OtherIOError });
            }
        }
    }
}
