using System;
using System.IO;
using System.Reflection;

namespace VSRAD.DebugServer.Logging
{
    static class GlobalLogger
    {
        private static readonly string _executableName;
        private static readonly string _assemblyName;
        private static readonly string _assemblyVersion;

        static GlobalLogger()
        {
            var assembly = Assembly.GetEntryAssembly();
            _executableName = Path.GetFileNameWithoutExtension(assembly.Location);
            _assemblyName = assembly.GetName().Name;
            _assemblyVersion = assembly.GetName().Version.ToString(3);
        }

        public static void ServerStarted(CliOptions options)
        {
            var verboseInfo = options.Verbose ? "Verbose mode." : "Use -v option to enable verbose mode.";
            Console.WriteLine($"{_assemblyName} {_assemblyVersion} is running on port {options.Port}. {verboseInfo}");
        }

        public static void Usage()
        {
            Console.WriteLine($"Usage: {_executableName} [port] [-v|--verbose]");
            Console.WriteLine("    port          (Default: 9339) Run the server on the specified TCP port");
            Console.WriteLine("    -v, --verbose Print stdout and stderr of executing commands");
        }
    }
}
