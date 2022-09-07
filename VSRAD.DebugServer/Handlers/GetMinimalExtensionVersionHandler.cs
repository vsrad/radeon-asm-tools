using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    class GetMinimalExtensionVersionHandler : IHandler
    {
        public GetMinimalExtensionVersionHandler(IPC.Commands.GetMinimalExtensionVersion _) { }

        public Task<IResponse> RunAsync()
            => Task.FromResult<IResponse>(new MinimalExtensionVersion {
                MinExtensionVersion = Server.MIN_EXT_VERSION,
                ServerVersion = typeof(GetMinimalExtensionVersionHandler).Assembly.GetName().Version.ToString(3)
            });
    }
}
