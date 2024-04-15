using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.Handlers;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.Logging;

namespace VSRAD.DebugServer
{
    public static class Dispatcher
    {
        public static Task<IResponse> DispatchAsync(ICommand command, ClientLogger clientLog) => command switch
        {
            Execute e => new ExecuteHandler(e, clientLog).RunAsync(),
            FetchMetadata fm => new FetchMetadataHandler(fm).RunAsync(),
            FetchResultRange frr => new FetchResultRangeHandler(frr).RunAsync(),
            ListEnvironmentVariables lev => new ListEnvironmentVariablesHandler(lev).RunAsync(),
            ListFilesCommand lf => new ListFilesHandler(lf).RunAsync(),
            GetFilesCommand gf => new GetFilesHandler(gf).RunAsync(),
            PutFilesCommand pf => new PutFilesHandler(pf).RunAsync(),
            ExchangeVersionsCommand ev => new ExchangeVersionsHandler(ev).RunAsync(),
            _ => throw new ArgumentException($"Unknown command type {command.GetType()}"),
        };
    }
}
