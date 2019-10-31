using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ListEnvironmentVariablesHandler : IHandler
    {
        public ListEnvironmentVariablesHandler(IPC.Commands.ListEnvironmentVariables _) { }

        public Task<IResponse> RunAsync()
        {
            var variables = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .ToDictionary((e) => (string)e.Key, (e) => (string)e.Value);

            return Task.FromResult<IResponse>(new EnvironmentVariablesListed { Variables = variables });
        }
    }

}
