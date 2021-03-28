using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;

namespace VSRAD.DebugServer
{
    public sealed class NetworkClient
    {
        public uint Id { get; }

        public EndPoint EndPoint => _socket.Client.RemoteEndPoint;

        private readonly TcpClient _socket;

        public NetworkClient(TcpClient socket, uint id)
        {
            Id = id;
            _socket = socket;
        }

        public void Disconnect() => _socket.Close();

        public async Task<(ICommand, int)> ReceiveCommandAsync()
        {
            try
            {
                var (message, size) = await _socket.GetStream().ReadSerializedCommandAsync<ICommand>().ConfigureAwait(false);
                if (message == null) throw new ConnectionFailedException();

                return (message, size);
            }
            catch (IOException e) when (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                throw new ConnectionFailedException();
            }
        }

        public Task<int> SendResponseAsync(IPC.Responses.IResponse response) =>
             _socket.GetStream().WriteSerializedMessageAsync(response);
    }
}
