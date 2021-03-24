using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

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
                var compLevel = _command.UseCompression ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
                using var memStream = new MemoryStream();

                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Update, false))
                {
                    foreach (var path in _command.Paths)
                    {
                        var fullPath = Path.Combine(rootPath, path);

                        if (path.EndsWith('/'))
                        {
                            var e = archive.CreateEntry(path);
                            e.LastWriteTime = Directory.GetLastWriteTimeUtc(fullPath);
                        }
                        else
                        {
                            var e = archive.CreateEntryFromFile(fullPath, path.Replace('\\', '/'), compLevel);
                            e.LastWriteTime = e.LastWriteTime.UtcDateTime;
                        }
                    }
                }

                return Task.FromResult<IResponse>(new GetFilesResponse { Status = GetFilesStatus.Successful, ZipData = memStream.ToArray() });
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
