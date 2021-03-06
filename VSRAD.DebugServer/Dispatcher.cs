﻿using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.Handlers;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public static class Dispatcher
    {
        public static Task<IResponse> DispatchAsync(ICommand command, Client client) => command switch
        {
            Execute e => new ExecuteHandler(e, client).RunAsync(),
            FetchMetadata fm => new FetchMetadataHandler(fm).RunAsync(),
            FetchResultRange frr => new FetchResultRangeHandler(frr).RunAsync(),
            PutFileCommand pf => new PutFileHandler(pf).RunAsync(),
            PutDirectoryCommand pd => new PutDirectoryHandler(pd).RunAsync(),
            ListFilesCommand lf => new ListFilesHandler(lf).RunAsync(),
            GetFilesCommand gf => new GetFilesHandler(gf).RunAsync(),
            GetServerCapabilitiesCommand gsc => new GetServerCapabilitiesHandler(gsc, client).RunAsync(),
            Deploy d => new DeployHandler(d, client.Log).RunAsync(),
            ListEnvironmentVariables lev => new ListEnvironmentVariablesHandler(lev).RunAsync(),
            _ => throw new ArgumentException($"Unknown command type {command.GetType()}"),
        };
    }
}
