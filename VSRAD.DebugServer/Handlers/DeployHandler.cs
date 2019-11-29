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
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(tempFile, _archive);

            using (var archive = ZipFile.Open(tempFile, ZipArchiveMode.Read))
            {
                var deployItems = archive.Entries.Select(entry => _destination + Path.DirectorySeparatorChar + entry.FullName);
                _log.DeployItemsReceived(deployItems);

                archive.ExtractToDirectory(_destination, overwriteFiles: true);
            }
            File.Delete(tempFile);

            return Task.FromResult<IResponse>(null);
        }
    }
}
