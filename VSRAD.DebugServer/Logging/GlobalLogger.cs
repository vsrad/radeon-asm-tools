using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace VSRAD.DebugServer.Logging
{
    public sealed class GlobalLogger
    {
        private static readonly string _executableName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        private static readonly string _assemblyName = Assembly.GetEntryAssembly().GetName().Name;
        private static readonly string _assemblyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);

        private static readonly string _logTemplateConsole = "[{Timestamp:HH:mm:ss} {Level:u3}]{Context} {Message:lj}{NewLine}{Exception}";
        private static readonly string _logTemplateFile = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}]{Context} {Message:lj}{NewLine}{Exception}";

        private readonly ILogger _logger;
        private readonly LoggingLevelSwitch _loggingLevel = new LoggingLevelSwitch();

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
                .MinimumLevel.ControlledBy(_loggingLevel)
                .WriteTo.Console(outputTemplate: _logTemplateConsole)
                .WriteTo.File($"RadeonAsmDebugServer_{_assemblyVersion}_.txt",
                    outputTemplate: _logTemplateFile,
                    rollingInterval: RollingInterval.Month,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        public void SetLogLevel(bool verbose) =>
            _loggingLevel.MinimumLevel = verbose ? LogEventLevel.Verbose : LogEventLevel.Information;

        public ClientLogger CreateClientLogger(uint clientId) =>
            new ClientLogger(_logger.ForContext("Context", $"[Client #{clientId}]"));

        public void ServerStartException(EndPoint localEndpoint, Exception e) =>
            _logger.Error(e, $"Failed to start the server on endpoint {localEndpoint}");

        public void ServerStarted(EndPoint localEndpoint)
        {
            var verboseInfo = _loggingLevel.MinimumLevel == LogEventLevel.Verbose ? "Verbose mode." : "Use -v option to enable verbose mode.";
            _logger.Information($"{_assemblyName} {_assemblyVersion} is listening on {localEndpoint}. {verboseInfo}");
        }

        public void Usage()
        {
            _logger.Information($"Usage: {_executableName} [endpoint] [-v|--verbose]");
            _logger.Information("    endpoint      (Default: 0.0.0.0:9339) Listen on the specified TCP endpoint");
            _logger.Information("    -v, --verbose Print stdout and stderr of executing commands");
        }
    }
}
