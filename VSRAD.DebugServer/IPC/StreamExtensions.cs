using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public static class StreamExtensions
    {
        public static async Task<T> ReadSerializedMessageAsync<T>(this Stream stream)
        {
            byte[] messageSizeBytes = new byte[4];
            if (await stream.ReadAsync(messageSizeBytes, 0, 4).ConfigureAwait(false) != 4)
            {
                return default;
            }
            int bytesNum = BitConverter.ToInt32(messageSizeBytes, 0);
            byte[] message = new byte[bytesNum];

            int buffered = 0;
            while (buffered != bytesNum)
            {
                var received = await stream.ReadAsync(message, buffered, bytesNum - buffered).ConfigureAwait(false);
                buffered += received;
                if (buffered != bytesNum && received == 0)
                {
                    return default;
                }
            }
            using (var memStream = new MemoryStream(message))
            using (var reader = new IPCReader(memStream))
            {
                return typeof(T) == typeof(ICommand) ? (T)reader.ReadCommand() : (T)reader.ReadResponse();
            }
        }

        public static async Task<int> WriteSerializedMessageAsync<T>(this Stream stream, T message)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new IPCWriter(memStream))
            {
                memStream.Position += sizeof(int); // the first four bytes are reserved for message length

                switch (message)
                {
                    case ICommand command: writer.WriteCommand(command); break;
                    case IResponse response: writer.WriteResponse(response); break;
                    default: throw new ArgumentException($"Unknown message type {typeof(T)}");
                }

                var prefixedMessageLength = (int)memStream.Length;
                var unprefixedMessageLength = prefixedMessageLength - sizeof(int);

                memStream.Position = 0;
                writer.Write(unprefixedMessageLength);

                // Use a single WriteAsync call to avoid sending multiple TCP packets for one command
                await stream.WriteAsync(memStream.GetBuffer(), 0, prefixedMessageLength).ConfigureAwait(false);
                return prefixedMessageLength;
            }
        }

        public static async Task<bool> ProcessDataTransfer(this Stream writer, ICommand command)
        {
            switch (command)
            {
                case SendFileCommand _:
                    var send_command = (SendFileCommand)command;
                    var sendPath = Path.Combine(send_command.SrcPath, send_command.Metadata.relativePath_);
                    return await SendFileAsync(writer, sendPath).ConfigureAwait(false);
                case GetFileCommand _:
                    var receive_command = (GetFileCommand)command;
                    var receivePath = Path.Combine(receive_command.DstPath, receive_command.Metadata.relativePath_);
                    return await ReceiveFileAsync(writer, receivePath);
                default:
                    break;
            }
            return true;
        }

        public static async Task<bool> SendFileAsync(this Stream writer, String path)
        {
            using (var reader = new FileStream(path, FileMode.Open))
            {
                var BUFFER_SIZE = 4096;
                byte[] buffer = new byte[BUFFER_SIZE];

                var bytesToSend = reader.Length;
                byte[] fileSizeBytes = BitConverter.GetBytes(bytesToSend);
                writer.Write(fileSizeBytes, 0, Convert.ToInt32(fileSizeBytes.GetLength(0)));

                while (bytesToSend > 0)
                {
                    var sendSize = Math.Min(bytesToSend, BUFFER_SIZE);

                    var readCount = reader.Read(buffer, 0, Convert.ToInt32(sendSize));
                    if (readCount == 0) throw new IOException("SendFileAsync file read error");

                    writer.Write(buffer, 0, readCount);

                    bytesToSend -= readCount;
                }
            }
            return true;
        }

        public static async Task<bool> ReceiveFileAsync(this Stream reader, String path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new FileStream(path, FileMode.Create))
            {
                var BUFFER_SIZE = 4096;
                byte[] buffer = new byte[BUFFER_SIZE];

                byte[] fileSizeBytes = new byte[sizeof(long)];
                reader.Read(fileSizeBytes, 0, Convert.ToInt32(fileSizeBytes.Length));
                var bytesToReceive = BitConverter.ToInt64(fileSizeBytes, 0);

                while (bytesToReceive > 0)
                {
                    var receiveSize = Convert.ToInt32(Math.Min(bytesToReceive, BUFFER_SIZE));

                    var receivedBytes = reader.Read(buffer, 0, Convert.ToInt32(receiveSize));
                    if (receivedBytes == 0) throw new IOException("ReceiveFileAsync network read error");

                    writer.Write(buffer, 0, Convert.ToInt32(receivedBytes));

                    bytesToReceive -= receivedBytes;
                }
            }
            return true;
        }
    }
}
