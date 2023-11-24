using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Serilog;

namespace VSRAD.DebugServer
{
    public class ClientLogger
    {
        private readonly uint _clientId;
        private readonly bool _verbose;
        private readonly Stopwatch _timer;

        public ClientLogger(uint clientId, bool verbose)
        {
            _clientId = clientId;
            _verbose = verbose;
            _timer = new Stopwatch();
        }

        public void ConnectionEstablished(EndPoint clientEndpoint) =>
            Print($"Connection with {clientEndpoint} has been established.");

        public void CommandReceived(IPC.Commands.ICommand c)
        {
            Print($"Command received: {c}");
            if (_verbose)
                _timer.Restart();
        }

        public void ResponseSent(IPC.Responses.IResponse r, int bytesSent) =>
            Print($"Sent response ({bytesSent} bytes): {r}");

        public void FatalClientException(Exception e) =>
            Print("An exception has occurred while processing the command. Connection has been terminated." + Environment.NewLine + e.ToString());

        public void CliendDisconnected() =>
            Log.Information($"{Environment.NewLine}client #{_clientId} has been disconnected");

        public void ExecutionStarted()
        {
            if (_verbose) Log.Information("===");
        }

        public void StdoutReceived(string output)
        {
            if (_verbose)
                Log.Information($"#{_clientId} stdout> " + output);
        }

        public void StderrReceived(string output)
        {
            if (_verbose)
                Log.Information($"#{_clientId} stderr> " + output);
        }

        public void DeployItemsReceived(IEnumerable<string> outputPaths)
        {
            if (!_verbose) return;

            Log.Information("Deploy Items:");
            foreach (var path in outputPaths)
                Log.Information("-- " + path);
        }

        public void CommandProcessed()
        {
            if (!_verbose) return;

            _timer.Stop();
            Log.Information($"{Environment.NewLine}Time Elapsed: {_timer.ElapsedMilliseconds}ms");
        }

        public void ParseVersionError(String version)
        {
            Log.Information($"{Environment.NewLine}Invalid Version on handshake attempt: {version}");
        }

        public void InvalidVersion(String receivedVersion, String minimalVersion)
        {
            Log.Information($"{Environment.NewLine}Version mismatch. Client version: {receivedVersion}," +
                $" expected version greater then {minimalVersion} ");
        }

        public void ClientRejectedServerVersion(String serverVersion, String clientVersion)
        {
            Log.Information($"{Environment.NewLine}Client rejected server version{Environment.NewLine}" +
                $"client version: {clientVersion}, server version: {serverVersion}");
        }

        public void HandshakeFailed(EndPoint clientEndpoint)
        {
            Log.Information($"{Environment.NewLine}Handshake in connection with {clientEndpoint} failed");
        }

        public void ConnectionTimeoutOnHandShake()
        {
            Log.Information($"{Environment.NewLine}Connection timeout on handshake attempt");
        }

        public void LockAcquired()
        {
            Log.Information($"{Environment.NewLine}client#{_clientId} acquired lock");
        }

        public void LockReleased()
        {
            Log.Information($"{Environment.NewLine}client#{_clientId} released lock");
        }

        private void Print(string message) =>
            Log.Information("===" + Environment.NewLine + $"[Client #{_clientId}] {message}");
    }
}
