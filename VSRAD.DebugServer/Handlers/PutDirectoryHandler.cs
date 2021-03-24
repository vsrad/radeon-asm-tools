using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutDirectoryHandler : IHandler
    {
        private readonly PutDirectoryCommand _command;

        public PutDirectoryHandler(PutDirectoryCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var fullPath = Path.Combine(_command.WorkDir, _command.Path);

            if (File.Exists(fullPath))
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.TargetPathIsFile });

            try
            {
                var destination = Directory.CreateDirectory(fullPath);

                using var stream = new MemoryStream(_command.ZipData);
                using var archive = new ZipArchive(stream);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    var entryDestPath = Path.Combine(destination.FullName, entry.FullName);
                    if (!entryDestPath.StartsWith(destination.FullName, StringComparison.Ordinal))
                        return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.ArchiveContainsPathOutsideTarget });

                    if (Path.GetFileName(entryDestPath.AsSpan()).IsEmpty)
                    {
                        Directory.CreateDirectory(entryDestPath);
                        if (_command.PreserveTimestamps)
                            Directory.SetLastWriteTimeUtc(entryDestPath, entry.LastWriteTime.DateTime);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(entryDestPath));
                        entry.ExtractToFile(entryDestPath, overwrite: true);
                        if (_command.PreserveTimestamps)
                            File.SetLastWriteTimeUtc(entryDestPath, entry.LastWriteTime.DateTime);
                    }
                }

                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.Successful });
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.PermissionDenied });
            }
            catch (IOException)
            {
                return Task.FromResult<IResponse>(new PutDirectoryResponse { Status = PutDirectoryStatus.OtherIOError });
            }
        }
    }
}
