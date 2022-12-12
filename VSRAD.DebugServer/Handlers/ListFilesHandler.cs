using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    class ListFilesHandler : IHandler
    {
        private readonly ListFilesCommand _command;

        public ListFilesHandler(ListFilesCommand command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var root = new DirectoryInfo(_command.DstPath);
            var files = new List<FileMetadata>();

            foreach (var info in root.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                var relativePath = PathExtension.GetRelativePath(_command.DstPath, info.FullName);
                files.Add(new FileMetadata(relativePath, info.LastWriteTimeUtc, info.Attributes.HasFlag(FileAttributes.Directory)));
            }
 
            return Task.FromResult<IResponse>(new ListFilesResponse { Files = files });
        }
    }
}