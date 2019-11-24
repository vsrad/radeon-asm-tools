using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.Handlers;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public static class Dispatcher
    {
        public static Task<IResponse> DispatchAsync(ICommand command, ClientLogger clientLog)
        {
            switch (command)
            {
                case Execute execute:
                    return new ExecuteHandler(execute, clientLog).RunAsync();
                case FetchMetadata fetchMetadata:
                    return new FetchMetadataHandler(fetchMetadata).RunAsync();
                case FetchResultRange fetchResultRange:
                    return new FetchResultRangeHandler(fetchResultRange).RunAsync();
                case Deploy deploy:
                    return new DeployHandler(deploy, clientLog).RunAsync();
                case ListEnvironmentVariables listEnvironmentVariables:
                    return new ListEnvironmentVariablesHandler(listEnvironmentVariables).RunAsync();
                default:
                    throw new ArgumentException($"Unknown command type {command.GetType()}");
            }
        }
    }
}
