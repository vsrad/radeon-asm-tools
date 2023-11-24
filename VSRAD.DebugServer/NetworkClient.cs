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

        public async Task<ICommand> ReceiveCommandAsync()
        {
            try
            {
                var message = await _socket.GetStream().ReadSerializedMessageAsync<ICommand>().ConfigureAwait(false);
                if (message == null) throw new ConnectionFailedException();

                return message;
            }
            catch (IOException e) when (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                throw new ConnectionFailedException();
            }
        }
        
        public NetworkStream GetStream()
        {
            return _socket.GetStream();
        }

        public Task<int> SendResponseAsync(IPC.Responses.IResponse response) =>
             _socket.GetStream().WriteSerializedMessageAsync(response);
    }
}
