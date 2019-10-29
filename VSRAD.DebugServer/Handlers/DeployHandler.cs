using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class DeployHandler : IHandler
    {
        private readonly string _destiination;
        private readonly byte[] _archive;

        public DeployHandler(IPC.Commands.Deploy command)
        {
            _archive = command.Data;
            _destiination = command.Destination;
        }

        public Task<IResponse> RunAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(tempFile, _archive);
            
            ZipFile.ExtractToDirectory(tempFile, _destiination, overwriteFiles: true);
            File.Delete(tempFile);

            return Task.FromResult<IResponse>(null);
        }
    }
}
