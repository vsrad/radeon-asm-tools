using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Reflection;

namespace VSRAD.DebugServer.Logging
{
    public sealed class GlobalLogger
    {
        public LoggingLevelSwitch LoggingLevel { get; } = new LoggingLevelSwitch();

        private static readonly string _executableName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        private static readonly string _assemblyName = Assembly.GetEntryAssembly().GetName().Name;
        private static readonly string _assemblyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);

        private static readonly string _logTemplateConsole = "[{Timestamp:HH:mm:ss} {Level:u3}]{Context} {Message:lj}{NewLine}{Exception}";
        private static readonly string _logTemplateFile = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}]{Context} {Message:lj}{NewLine}{Exception}";

        private readonly ILogger _logger;

        public GlobalLogger()
        {
            var cwd = Directory.GetCurrentDirectory();
            try
            {
                _ = File.Create(Path.Combine(cwd, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
            }
            catch (Exception)
            {
                Console.WriteLine($"WARNING: {Environment.NewLine} " +
                    $"RAD Debug Server is unable to save log file, because directory({cwd}) is read - only.{Environment.NewLine}" +
                    $"This server instance will display logs only in this window, no log - file will be created.");
            }

            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LoggingLevel)
                .WriteTo.Console(outputTemplate: _logTemplateConsole)
                .WriteTo.File($"RadeonAsmDebugServer_{_assemblyVersion}_.txt",
                    outputTemplate: _logTemplateFile,
                    rollingInterval: RollingInterval.Month,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        public ClientLogger CreateClientLogger(uint clientId) =>
            new ClientLogger(_logger.ForContext("Context", $"[Client #{clientId}]"));

        public void ServerStarted(CliOptions options)
        {
            LoggingLevel.MinimumLevel = options.Verbose ? LogEventLevel.Verbose : LogEventLevel.Information;
            var verboseInfo = options.Verbose ? "Verbose mode." : "Use -v option to enable verbose mode.";
            _logger.Information($"{_assemblyName} {_assemblyVersion} is running on port {options.Port}. {verboseInfo}");
        }

        public void Usage()
        {
            _logger.Information($"Usage: {_executableName} [port] [-v|--verbose]");
            _logger.Information("    port          (Default: 9339) Run the server on the specified TCP port");
            _logger.Information("    -v, --verbose Print stdout and stderr of executing commands");
        }
    }
}
