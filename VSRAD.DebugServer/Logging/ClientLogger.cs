using System;
using System.IO;
using System.Net;

namespace VSRAD.DebugServer
{
    public class ClientLogger
    {
        private readonly uint _clientId;
        private readonly bool _verbose;

        public ClientLogger(uint clientId, bool verbose)
        {
            _clientId = clientId;
            _verbose = verbose;
        }

        public void ConnectionEstablished(EndPoint clientEndpoint) =>
            Print($"Connection with {clientEndpoint} has been established.");

        public void CommandReceived(IPC.Commands.ICommand c) =>
            Print($"Command received: {c}");

        public void ResponseSent(IPC.Responses.IResponse r, int bytesSent) =>
            Print($"Sent response ({bytesSent} bytes): {r}");

        public void FatalClientException(Exception e)
        {
            if (e is IOException)
                Print("Connection has been terminated.");
            else
                Print("An exception has occurred while processing the command. Connection has been terminated."+ Environment.NewLine + e.ToString());
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

        private void Print(string message) =>
            Console.WriteLine("===" + Environment.NewLine + $"[Client #{_clientId}] {message}");
    }
}
