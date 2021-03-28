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

        public async Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.WorkDir, _command.Path);

            if (File.Exists(fullPath))
                return new PutDirectoryResponse { Status = PutDirectoryStatus.TargetPathIsFile };

            bool retryOnce = true;
            while (true)
            {
                try
                {
                    PackedFile.UnpackFiles(fullPath, _command.Files, _command.DecompressFiles, _command.PreserveTimestamps);
                    return new PutDirectoryResponse { Status = PutDirectoryStatus.Successful };
                }
                catch (UnauthorizedAccessException)
                {
                    return new PutDirectoryResponse { Status = PutDirectoryStatus.PermissionDenied };
                }
                catch (IOException)
                {
                    // Retrying the operation helps with "file is being used by another process" errors
                    // when the process that accessed the file has just exited
                    if (retryOnce)
                    {
                        retryOnce = false;
                        await Task.Delay(100);
                    }
                    else
                    {
                        return new PutDirectoryResponse { Status = PutDirectoryStatus.OtherIOError };
                    }
                }
            }
        }
    }
}
