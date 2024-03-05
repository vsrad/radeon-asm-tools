using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ExchangeVersionsHandler : IHandler
    {
        private static readonly Version _serverVersion = typeof(ExchangeVersionsHandler).Assembly.GetName().Version;
        private static readonly OSPlatform _serverPlatform =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux :
            throw new PlatformNotSupportedException("Only Windows and Linux server platforms are currently supported");

        private readonly Version _clientVersion;

        public ExchangeVersionsHandler(IPC.Commands.ExchangeVersionsCommand command)
        {
            _clientVersion = command.ClientVersion;
        }

        public Task<IResponse> RunAsync()
        {
            var status = _clientVersion >= Server.MinimumClientVersion ? ExchangeVersionsStatus.Successful : ExchangeVersionsStatus.ClientVersionUnsupported;
            return Task.FromResult<IResponse>(new ExchangeVersionsResponse { Status = status, ServerVersion = _serverVersion, ServerPlatform = _serverPlatform });
        }
    }
}