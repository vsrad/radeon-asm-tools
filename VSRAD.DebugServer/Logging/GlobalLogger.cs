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

            var cwd = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            try
            {
                var fs = File.Create(
                    Path.Combine(
                        cwd,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose);
            } catch (Exception)
            {
                Console.WriteLine($"WARNING: {Environment.NewLine} " +
                    $"RAD Debug Server is unable to save log file, because directory({cwd}) is read - only.{Environment.NewLine}" +
                    $"This server instance will display logs only in this window, no log - file will be created.");
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File($"RadeonAsmDebugServer_{_assemblyVersion}_.txt",
                    rollingInterval: RollingInterval.Month,
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
