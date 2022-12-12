using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class CheckOutdatedFilesHandler : IHandler
    {
        private readonly CheckOutdatedFiles _command;

        public CheckOutdatedFilesHandler(CheckOutdatedFiles command)
        {
            _command = command;
        }

        public Task<IResponse> RunAsync()
        {
            var files = new List<FileMetadata>();
            
            foreach (var info in _command.Files)
            {
                if (FileMetadata.isOutdated(info, _command.DstPath))
                    continue;

                files.Add(info);
            }

            return Task.FromResult<IResponse>(new CheckOutdatedFilesResponse { Files = files });
        }
    }
}
