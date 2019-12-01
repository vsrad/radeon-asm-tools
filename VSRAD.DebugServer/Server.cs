using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.DebugServer
{
    public sealed class Server
    {
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
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                ClientConnected(client, clientsCount);
                clientsCount++;
            }
        }

        private void ClientConnected(TcpClient tcpClient, uint clientId)
        {
            var networkClient = new NetworkClient(tcpClient, clientId);
            var clientLog = new ClientLogger(clientId, _verboseLogging);
            clientLog.ConnectionEstablished(networkClient.EndPoint);
            Task.Run(() => BeginClientLoopAsync(networkClient, clientLog));
        }

        private async Task BeginClientLoopAsync(NetworkClient client, ClientLogger clientLog)
        {
            while (true)
            {
                bool lockAcquired = false;
                try
                {
                    var command = await client.ReceiveCommandAsync().ConfigureAwait(false);
                    clientLog.CommandReceived(command);

                    await _commandExecutionLock.WaitAsync();
                    lockAcquired = true;

                    var response = await Dispatcher.DispatchAsync(command, clientLog).ConfigureAwait(false);
                    if (response != null) // commands like Deploy do not return a response
                    {
                        var bytesSent = await client.SendResponseAsync(response).ConfigureAwait(false);
                        clientLog.ResponseSent(response, bytesSent);
                    }
                    clientLog.CommandProcessed();
                }
                catch (ConnectionFailedException)
                {
                    client.Disconnect();
                    clientLog.CliendDisconnected();
                    break;
                }
                catch (Exception e)
                {
                    client.Disconnect();
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
