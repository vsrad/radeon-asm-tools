using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;

namespace VSRAD.DebugServer
{
    public sealed class Server
    {
        public static readonly Version MinimumClientVersion = new Version("2024.3.3");

        private readonly SemaphoreSlim _commandExecutionLock = new SemaphoreSlim(1, 1);
        private readonly TcpListener _listener;
        private readonly bool _verboseLogging;

        public Server(IPAddress ip, int port, bool verboseLogging = false)
        {
            _listener = new TcpListener(ip, port);
            _verboseLogging = verboseLogging;
        }

        public async Task LoopAsync()
        {
            _listener.Start();
            uint clientsCount = 0;
            while (true)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var clientId = clientsCount;
                clientsCount++;
                _ = Task.Run(() => BeginClientLoopAsync(clientId, tcpClient));
            }
        }

        private async Task BeginClientLoopAsync(uint clientId, TcpClient tcpClient)
        {
            using (tcpClient)
            {
                var clientLog = new ClientLogger(clientId, _verboseLogging);
                clientLog.ConnectionEstablished(tcpClient.Client.RemoteEndPoint);
                while (true)
                {
                    var lockAcquired = false;
                    try
                    {
                        var (command, bytesReceived) = await tcpClient.GetStream().ReadSerializedCommandAsync<ICommand>().ConfigureAwait(false);
                        clientLog.CommandReceived(command, bytesReceived);

                        await _commandExecutionLock.WaitAsync();
                        lockAcquired = true;

                        var response = await Dispatcher.DispatchAsync(command, clientLog).ConfigureAwait(false);
                        if (response != null) // commands like Deploy do not return a response
                        {
                            var bytesSent = await tcpClient.GetStream().WriteSerializedMessageAsync(response).ConfigureAwait(false);
                            clientLog.ResponseSent(response, bytesSent);
                        }
                        clientLog.CommandProcessed();
                    }
                    catch (Exception e)
                    {
                        if (e is OperationCanceledException || e is EndOfStreamException || (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset))
                            clientLog.CliendDisconnected();
                        else
                            clientLog.FatalClientException(e);
                        break;
                    }
                    finally
                    {
                        if (lockAcquired)
                            _commandExecutionLock.Release();
                    }
                }
            }
        }
    }
}
