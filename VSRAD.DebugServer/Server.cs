using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.DebugServer
{
    public sealed class Server
    {
        private readonly SemaphoreSlim _commandExecutionLock = new SemaphoreSlim(1, 1);
        private readonly TcpListener _listener;
        private readonly bool _verboseLogging;

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

        public void HandleStdin()
        {
            while (true)
            {
                if (Console.KeyAvailable) Console.Read();
            }
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
            new Task(HandleStdin).Start();
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
