using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using VSRAD.DebugServer.IPC.Commands;
namespace VSRAD.DebugServer
{
    public sealed class Server
    {
        private readonly SemaphoreSlim _commandExecutionLock = new SemaphoreSlim(1, 1);
        private readonly TcpListener _listener;
        private readonly bool _verboseLogging;
        private static Version _serverVersion = typeof(Server).Assembly.GetName().Version;
        private static Version _minimalAcceptedClientVersion = new Version("2021.12.8");

        const uint ENABLE_QUICK_EDIT = 0x0040;
        const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint lpMode);
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        public Server(IPAddress ip, int port, bool verboseLogging = false)
        {
            _listener = new TcpListener(ip, port);
            _verboseLogging = verboseLogging;
        }

        public async Task LoopAsync()
        {
            /* disable Quick Edit cmd feature to prevent server hanging */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
                UInt32 consoleMode;
                if (!GetConsoleMode(consoleHandle, out consoleMode))
                    Console.WriteLine($"Warning! Cannot get console mode. Error code={GetLastError()}");
                consoleMode &= ~ENABLE_QUICK_EDIT;
                if (!SetConsoleMode(consoleHandle, consoleMode))
                    Console.WriteLine($"Warning! Cannot set console mode. Error code={GetLastError()}");
            }
            _listener.Start();
            uint clientsCount = 0;
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                TcpClientConnected(client, clientsCount);
                clientsCount++;
            }
        }

        private void TcpClientConnected(TcpClient tcpClient, uint clientId)
        {
            var networkClient = new NetworkClient(tcpClient, clientId);
            var clientLog = new ClientLogger(clientId, _verboseLogging);
            if (!Task.Run(() => TryProcessServerHandshake(tcpClient, clientLog)).Result)
            {
                clientLog.HandshakeFailed(networkClient.EndPoint);
                return;
            }
            clientLog.ConnectionEstablished(networkClient.EndPoint);
            Task.Run(() => BeginClientLoopAsync(networkClient, clientLog));
        }

        private async Task<bool> TryProcessServerHandshake(TcpClient client, ClientLogger clientLog)
        {
            try
            {
                var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
                var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

                // Send server version to client
                //
                await writer.WriteLineAsync(_serverVersion.ToString()).ConfigureAwait(false);

                // Obtain client version
                //
                String clientResponse = await reader.ReadLineAsync().ConfigureAwait(false);

                if (!Version.TryParse(clientResponse, out var clientVersion))
                {
                    clientLog.ParseVersionError(clientResponse);
                    // Inform client that server declines client's version
                    //
                    await writer.WriteLineAsync(HandShakeStatus.ServerNotAccepted.ToString()).ConfigureAwait(false);
                    return false;
                }

                if (clientVersion.CompareTo(_minimalAcceptedClientVersion) < 0)
                {
                    clientLog.InvalidVersion(clientVersion.ToString(), _minimalAcceptedClientVersion.ToString());
                    // Inform client that server declines client's version
                    //
                    await writer.WriteLineAsync(HandShakeStatus.ServerNotAccepted.ToString()).ConfigureAwait(false);
                    return false;
                }

                // Inform client that server accepts client's version
                //
                await writer.WriteLineAsync(HandShakeStatus.ServerAccepted.ToString()).ConfigureAwait(false);

                // Check if client accepts server version
                //
                if (await reader.ReadLineAsync() != HandShakeStatus.ClientAccepted.ToString())
                {
                    clientLog.ClientRejectedServerVersion(_serverVersion.ToString(), clientVersion.ToString());
                    return false;
                }
            } catch (Exception)
            {
                clientLog.ConnectionTimeoutOnHandShake();
                return false;
            }
            return true;
        }

        private async Task BeginClientLoopAsync(NetworkClient client, ClientLogger clientLog)
        {
            while (true)
            {
                var lockAcquired = false;
                try
                {
                    var command = await client.ReceiveCommandAsync().ConfigureAwait(false);
                    clientLog.CommandReceived(command);

                    await _commandExecutionLock.WaitAsync();
                    lockAcquired = true;

                    var response = await Dispatcher.DispatchAsync(command, clientLog, client).ConfigureAwait(false);
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
