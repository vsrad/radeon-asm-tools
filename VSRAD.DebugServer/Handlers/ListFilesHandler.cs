using System;
using System.Collections.Generic;
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

            var files = new List<(string RelativePath, bool IsDirectory, long Size, DateTime Timestamp)>();

            if (Directory.Exists(path))
            {
                files.Add((".", true, 0, File.GetLastWriteTimeUtc(path)));

                var root = new DirectoryInfo(path);
                foreach (var entry in root.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    var relPath = entry.FullName.Substring(root.FullName.Length + 1);
                    if (entry is FileInfo file)
                        files.Add((relPath, false, file.Length, file.LastWriteTimeUtc));
                    else
                        files.Add((relPath, true, 0, entry.LastWriteTimeUtc));
                }
            }
            else if (File.Exists(path))
            {
                var file = new FileInfo(path);
                files.Add((".", false, file.Length, file.LastWriteTimeUtc));
            }

            return Task.FromResult<IResponse>(new ListFilesResponse { Files = files.ToArray() });
        }
    }
}
