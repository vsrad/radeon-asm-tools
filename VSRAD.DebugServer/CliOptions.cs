using System.Net;

namespace VSRAD.DebugServer
{
    public sealed class CliOptions
    {
        private static readonly IPEndPoint _defaultEndpoint = new IPEndPoint(IPAddress.Any, 9339);

        public IPEndPoint LocalEndpoint { get; }
        public bool Verbose { get; }

        private CliOptions(IPEndPoint localEndpoint, bool verbose)
        {
            LocalEndpoint = localEndpoint;
            Verbose = verbose;
        }

        public static bool TryParse(string[] args, out CliOptions options)
        {
            var endpoint = _defaultEndpoint;
            var verbose = false;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "-v":
                    case "--verbose":
                        verbose = true;
                        continue;
                    default:
                        if (IPEndPoint.TryParse(arg, out endpoint))
                            continue;

                        options = null;
                        return false;
                }
            }

            options = new CliOptions(endpoint, verbose);
            return true;
        }
    }
}
