using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class GetServerCapabilitiesHandler : IHandler
    {
        public Task<IResponse> RunAsync()
        {
            var info = new CapabilityInfo(
                version: typeof(CapabilityInfo).Assembly.GetName().Version.ToString(3),
                platform: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ServerPlatform.Windows : ServerPlatform.Linux,
                capabilities: CapabilityInfo.LatestServerCapabilities
            );
            return Task.FromResult<IResponse>(new GetServerCapabilitiesResponse { Info = info });
        }
    }
}
