using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public sealed class Client
    {
        public HashSet<ExtensionCapability> Capabilities { get; } = new HashSet<ExtensionCapability>();

        public ClientLogger Log { get; }

        private readonly TcpClient _socket;
        private readonly SemaphoreSlim _globalCommandLock;
        private readonly SemaphoreSlim _handlerStreamUsageLock = new SemaphoreSlim(1, 1);

        public Client(uint clientId, TcpClient socket, SemaphoreSlim globalCommandLock, bool verboseLogging)
        {
            Log = new ClientLogger(clientId, verboseLogging);
            _socket = socket;
            _globalCommandLock = globalCommandLock;
        }

        public async Task<T> RespondWithFollowUpAsync<T>(IResponse response) where T : ICommand
        {
            try
            {
                await _handlerStreamUsageLock.WaitAsync();
                await SendResponseAsync(response);
                return (T)(await ReadCommandAsync());
            }
            finally
            {
                _handlerStreamUsageLock.Release();
            }
        }

        public async Task PingAsync()
        {
            if (Capabilities.Contains(ExtensionCapability.Base)) // If the client does not support pings, treat it as a noop
            {
                try
                {
                    await _handlerStreamUsageLock.WaitAsync();
                    // Pinging from the handler is safe: the client is waiting for the response and isn't going to send any data
                    await _socket.GetStream().PingUnsafeAsync();
                }
                finally
                {
                    _handlerStreamUsageLock.Release();
                }
            }
        }

        public async Task BeginClientLoopAsync()
        {
            Log.ConnectionEstablished(_socket.Client.RemoteEndPoint);
            while (true)
            {
                bool lockAcquired = false;
                try
                {
                    var command = await ReadCommandAsync().ConfigureAwait(false);

                    await _globalCommandLock.WaitAsync();
                    lockAcquired = true;

                    var response = await Dispatcher.DispatchAsync(command, this).ConfigureAwait(false);
                    if (response != null) // commands like Deploy do not return a response
                        await SendResponseAsync(response).ConfigureAwait(false);

                    Log.CommandProcessed();
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException || e is EndOfStreamException || (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset))
                        Log.CliendDisconnected();
                    else
                        Log.FatalClientException(e);

                    _socket.Close();
                    break;
                }
                finally
                {
                    if (lockAcquired)
                        _globalCommandLock.Release();
                }
            }
        }

        private async Task<ICommand> ReadCommandAsync()
        {
            var (command, bytesReceived) = await _socket.GetStream().ReadSerializedCommandAsync<ICommand>().ConfigureAwait(false);
            if (command != null)
                Log.CommandReceived(command, bytesReceived);

            return command;
        }

        private async Task SendResponseAsync(IResponse response)
        {
            var bytesSent = await _socket.GetStream().WriteSerializedMessageAsync(response).ConfigureAwait(false);
            Log.ResponseSent(response, bytesSent);
        }
    }
}
