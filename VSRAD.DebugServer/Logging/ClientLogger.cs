using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace VSRAD.DebugServer.Logging
{
    public class ClientLogger
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _timer = new Stopwatch();

        public ClientLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void ConnectionEstablished(EndPoint clientEndpoint) =>
            _logger.Information($"Connection established with {clientEndpoint}");

        public void CommandReceived(IPC.Commands.ICommand c, int bytesReceived)
        {
            _logger.Information($"Command received ({bytesReceived} bytes): {c}");
            _timer.Restart();
        }

        public void ResponseSent(IPC.Responses.IResponse r, int bytesSent) =>
            _logger.Information($"Sent response ({bytesSent} bytes): {r}");

        public void CommandProcessed()
        {
            _timer.Stop();
            _logger.Verbose($"Command processed in {_timer.ElapsedMilliseconds}ms");
        }

        public void ClientMissingVersionExchange(Version minimumClientVersion) =>
            _logger.Warning($"Client did not initiate the connection with version exchange. Connection rejected. Update the client to version {minimumClientVersion} or above");

        public void FatalClientException(Exception e) =>
            _logger.Error(e, "An exception occurred while processing the command. Connection terminated");

        public void CliendDisconnected() =>
            _logger.Information("Client disconnected");

        public void ExecutionStarted() { }

        public void StdoutReceived(string output) =>
            _logger.Verbose($"stdout> " + output);

        public void StderrReceived(string output) =>
            _logger.Verbose($"stderr> " + output);

        public void DeployItemsReceived(IEnumerable<string> outputPaths)
        {
            _logger.Verbose("Deploy Items:");
            foreach (var path in outputPaths)
                _logger.Verbose("-- " + path);
        }
    }
}
