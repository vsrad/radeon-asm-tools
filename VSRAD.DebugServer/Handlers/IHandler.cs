using System.Threading.Tasks;

namespace VSRAD.DebugServer.Handlers
{
    public interface IHandler
    {
        Task<IPC.Responses.IResponse> RunAsync();
    }
}
