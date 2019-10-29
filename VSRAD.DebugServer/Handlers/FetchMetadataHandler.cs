using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class FetchMetadataHandler : IHandler
    {
        private readonly bool BinaryOutput;
        private readonly string FilePath;

        public FetchMetadataHandler(IPC.Commands.FetchMetadata command)
        {
            BinaryOutput = command.BinaryOutput;
            FilePath = Path.Combine(command.FilePath);
        }

        public Task<IResponse> RunAsync()
        {
            if (!File.Exists(FilePath))
            {
                return Task.FromResult<IResponse>(new MetadataFetched { Status = FetchStatus.FileNotFound });
            }
            var timestamp = File.GetLastWriteTime(FilePath).ToUniversalTime();
            return Task.FromResult<IResponse>(new MetadataFetched
            {
                Status = FetchStatus.Successful,
                Timestamp = timestamp,
                ByteCount = GetByteCount(FilePath, BinaryOutput)
            });
        }

        private static int GetByteCount(string filePath, bool binaryOutput)
        {
            if (binaryOutput)
            {
                return (int)new FileInfo(filePath).Length;
            }
            else
            {
                int dwordCount = File.ReadLines(filePath).Count(s => !string.IsNullOrWhiteSpace(s)) - 1 /* skip metadata line */;
                return dwordCount * 4;
            }
        }
    }
}
