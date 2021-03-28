using System;
using System.IO;
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
            var rootPath = Path.Combine(_command.RootPath);
            try
            {
                var files = PackedFile.PackFiles(rootPath, _command.Paths);

                IResponse response = new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files };
                if (_command.UseCompression)
                    response = new CompressedResponse(response);

                return Task.FromResult(response);
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult<IResponse>(new GetFilesResponse { Status = GetFilesStatus.FileNotFound });
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult<IResponse>(new GetFilesResponse { Status = GetFilesStatus.FileNotFound });
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult<IResponse>(new GetFilesResponse { Status = GetFilesStatus.FileNotFound });
            }
            catch (IOException)
            {
                return Task.FromResult<IResponse>(new GetFilesResponse { Status = GetFilesStatus.OtherIOError });
            }
        }
    }
}
