namespace VSRAD.DebugServer
{
    public sealed class CliOptions
    {
        private const int _defaultPort = 9339;

        public int Port { get; }
        public bool Verbose { get; }

        private CliOptions(int port, bool verbose)
        {
            Port = port;
            Verbose = verbose;
        }

        public static bool TryParse(string[] args, out CliOptions options)
        {
            var port = _defaultPort;
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
                        if (int.TryParse(arg, out port))
                            continue;

                        options = null;
                        return false;
                }
            }

            options = new CliOptions(port, verbose);
            return true;
        }
    }
}
