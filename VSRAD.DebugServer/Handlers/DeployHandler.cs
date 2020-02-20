using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class DeployHandler : IHandler
    {
        private readonly ClientLogger _log;
        private readonly string _destination;
        private readonly byte[] _archive;

        public DeployHandler(IPC.Commands.Deploy command, ClientLogger log)
        {
            _log = log;
            _archive = command.Data;
            _destination = command.Destination;
        }

        public Task<IResponse> RunAsync()
        {
            using var stream = new MemoryStream(_archive);
            using var archive = new ZipArchive(stream);

            var deployItems = archive.Entries.Select(entry => _destination + Path.DirectorySeparatorChar + entry.FullName);
            _log.DeployItemsReceived(deployItems);

            archive.ExtractToDirectory(_destination, overwriteFiles: true);

            return Task.FromResult<IResponse>(null);
        }
    }
}
