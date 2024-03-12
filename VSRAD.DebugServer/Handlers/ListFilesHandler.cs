using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

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
            var files = FileMetadata.GetMetadataForPath(_command.Path, _command.IncludeSubdirectories);
            var response = new ListFilesResponse { Files = files.ToArray() };
            return Task.FromResult<IResponse>(new CompressedResponse(response));
        }
    }
}
