using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutFileHandler : IHandler
    {
        private readonly PutFileCommand _command;

        public PutFileHandler(PutFileCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.WorkDir, _command.Path);
            var status = DoPutFile(fullPath, _command.Data);
            return Task.FromResult<IResponse>(new PutFileResponse { Status = status });
        }

        private static PutFileStatus DoPutFile(string fullPath, byte[] data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, data);
                return PutFileStatus.Successful;
            }
            catch (UnauthorizedAccessException)
            {
                return PutFileStatus.PermissionDenied;
            }
            catch (IOException)
            {
                return PutFileStatus.OtherIOError;
            }
        }
    }
}
