using System.Net;
using System.Threading.Tasks;

namespace VSRAD.DebugServer
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (CliOptions.TryParse(args, out var options))
                Start(options);
            else
                Logging.GlobalLogger.Usage();
        }

        static void Start(CliOptions options)
        {
            var server = new Server(IPAddress.Any, options.Port, options.Verbose);
            Logging.GlobalLogger.ServerStarted(options);
            Task.Run(server.LoopAsync).Wait();
        }
    }
}
