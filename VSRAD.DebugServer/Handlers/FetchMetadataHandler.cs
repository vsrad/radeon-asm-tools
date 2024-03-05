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
            FilePath = command.FilePath;
        }

        public Task<IResponse> RunAsync()
        {
            if (!File.Exists(FilePath))
            {
                return Task.FromResult<IResponse>(new MetadataFetched { Status = FetchStatus.FileNotFound });
            }
            var timestamp = File.GetLastWriteTimeUtc(FilePath);
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
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
                using var reader = new StreamReader(stream);

                int dwordCount = 0;
                while (!string.IsNullOrWhiteSpace(reader.ReadLine()))
                    dwordCount++;
                return dwordCount * 4;
            }
        }
    }
}
