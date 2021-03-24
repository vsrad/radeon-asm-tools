using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ListFilesHandler : IHandler
    {
        private readonly ListFilesCommand _command;

        public ListFilesHandler(ListFilesCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var path = Path.Combine(_command.WorkDir, _command.Path);
            var files = FileMetadata.GetMetadataForTree(path);
            return Task.FromResult<IResponse>(new ListFilesResponse { Files = files.ToArray() });
        }
    }
}
