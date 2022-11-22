using System;
using System.IO;
using System.Reflection;
using Serilog;
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
            var log = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("log.txt",
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        public static void ServerStarted(CliOptions options)
        {
            var verboseInfo = options.Verbose ? "Verbose mode." : "Use -v option to enable verbose mode.";
            Log.Information($"{_assemblyName} {_assemblyVersion} is running on port {options.Port}. {verboseInfo}");
        }

        public static void Usage()
        {
            Log.Information($"Usage: {_executableName} [port] [-v|--verbose]");
            Log.Information("    port          (Default: 9339) Run the server on the specified TCP port");
            Log.Information("    -v, --verbose Print stdout and stderr of executing commands");
        }
    }
}
