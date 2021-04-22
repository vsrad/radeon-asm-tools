using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class GetServerCapabilitiesHandler : IHandler
    {
        private readonly GetServerCapabilitiesCommand _command;
        private readonly Client _client;

        public GetServerCapabilitiesHandler(GetServerCapabilitiesCommand command, Client client)
        {
            _command = command;
            _client = client;
        }

        public Task<IResponse> RunAsync()
        {
            foreach (var cap in _command.ExtensionCapabilities)
                _client.Capabilities.Add(cap);

            var info = new CapabilityInfo(
                version: typeof(CapabilityInfo).Assembly.GetName().Version.ToString(3),
                platform: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ServerPlatform.Windows : ServerPlatform.Linux,
                capabilities: CapabilityInfo.LatestServerCapabilities
            );

            return Task.FromResult<IResponse>(new GetServerCapabilitiesResponse { Info = info });
        }
    }
}
