using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

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
            Console.WriteLine($"{Environment.NewLine}client #{_clientId} has been disconnected");

        public void ExecutionStarted()
        {
            if (_verbose) Console.WriteLine("===");
        }

        public void StdoutReceived(string output)
        {
            if (_verbose)
                Console.WriteLine($"#{_clientId} stdout> " + output);
        }

        public void StderrReceived(string output)
        {
            if (_verbose)
                Console.WriteLine($"#{_clientId} stderr> " + output);
        }

        public void DeployItemsReceived(IEnumerable<string> outputPaths)
        {
            if (!_verbose) return;

            Console.WriteLine("Deploy Items:");
            foreach (var path in outputPaths)
                Console.WriteLine("-- " + path);
        }

        public void CommandProcessed()
        {
            if (!_verbose) return;

            _timer.Stop();
            Console.WriteLine($"{Environment.NewLine}Time Elapsed: {_timer.ElapsedMilliseconds}ms");
        }

        public void ParseVersionError(String version)
        {
            Console.WriteLine($"{Environment.NewLine}Invalid Version on handshake attempt: {version}");
        }

        public void InvalidVersion(String receivedVersion, String minimalVersion)
        {
            Console.WriteLine($"{Environment.NewLine}Version mismatch. Client version: {receivedVersion}," +
                $" expected version greater then {minimalVersion} ");
        }

        public void ClientRejectedServerVersion(String serverVersion, String clientVersion)
        {
            Console.WriteLine($"{Environment.NewLine}Client rejected server version{Environment.NewLine}" +
                $"client version: {clientVersion}, server version: {serverVersion}");
        }

        public void HandshakeFailed(EndPoint clientEndpoint)
        {
            Console.WriteLine($"{Environment.NewLine}Handshake in connection with {clientEndpoint} failed");
        }

        public void ConnectionTimeoutOnHandShake()
        {
            Console.WriteLine($"{Environment.NewLine}Connection timeout on handshake attempt");
        }

        public void LockAcquired()
        {
            Console.WriteLine($"{Environment.NewLine}client#{_clientId} acquired lock");
        }

        public void LockReleased()
        {
            Console.WriteLine($"{Environment.NewLine}client#{_clientId} released lock");
        }

        private void Print(string message) =>
            Console.WriteLine("===" + Environment.NewLine + $"[Client #{_clientId}] {message}");
    }
}
