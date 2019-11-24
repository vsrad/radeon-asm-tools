using System;
using System.Diagnostics;
using System.IO;
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

        public void FatalClientException(Exception e)
        {
            if (e is IOException)
                Print("Connection has been terminated.");
            else
                Print("An exception has occurred while processing the command. Connection has been terminated." + Environment.NewLine + e.ToString());
        }

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

        public void DeployItemsReceived(string[] outputPaths)
        {
            if (!_verbose) return;

            Console.WriteLine("Deploy Items:");
            for (var i = 0; i < outputPaths.Length; i++)
            {
                Console.WriteLine("-- " + outputPaths[i]);
            }
        }

        public void CommandProcessed()
        {
            if (!_verbose) return;

            _timer.Stop();
            Console.WriteLine($"{Environment.NewLine}Time Elapsed: {_timer.ElapsedMilliseconds}ms");
        }

        private void Print(string message) =>
            Console.WriteLine("===" + Environment.NewLine + $"[Client #{_clientId}] {message}");
    }
}
