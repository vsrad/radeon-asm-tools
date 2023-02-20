using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using K4os.Compression.LZ4;

namespace VSRAD.DebugServer
{
    public static class StreamExtensions
    {
        const int BUFFER_SIZE = 2097152;
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
                    return await SendFileAsync(writer, sendPath, send_command.UseCompression).ConfigureAwait(false);
                case GetFileCommand _:
                    var receive_command = (GetFileCommand)command;
                    var receivePath = Path.Combine(receive_command.DstPath, receive_command.Metadata.relativePath_);
                    return await ReceiveFileAsync(writer, receivePath, receive_command.UseCompression);
                default:
                    break;
            }
            return true;
        }

        public static async Task<bool> SendFileAsync(this Stream writer, String path, bool useCompression)
        {
            using (var reader = new FileStream(path, FileMode.Open))
            {
                var buffer = new byte[BUFFER_SIZE];

                var bytesToSend = reader.Length;
                var fileSizeBytes = BitConverter.GetBytes((long)bytesToSend);
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

        public static async Task<bool> SendCompressedFileAsync(this Stream writer, String path)
        {
            using (var reader = new FileStream(path, FileMode.Open))
            {
                var decodedbuffer = new byte[BUFFER_SIZE];
                var encodedBuffer = new byte[LZ4Codec.MaximumOutputSize(decodedbuffer.Length)];

                var bytesToSend = reader.Length;
                var fileSizeBytes = BitConverter.GetBytes((long)bytesToSend);
                writer.Write(fileSizeBytes, 0, Convert.ToInt32(fileSizeBytes.GetLength(0)));

                while (bytesToSend > 0)
                {
                    var sendSize = Math.Min(bytesToSend, BUFFER_SIZE);

                    var readCount = reader.Read(decodedbuffer, 0, Convert.ToInt32(sendSize));

                    if (readCount == 0) throw new IOException("SendFileAsync file read error");

                    var encodedLength = LZ4Codec.Encode(decodedbuffer, 0, decodedbuffer.Length,
                        encodedBuffer, 0, encodedBuffer.Length);

                    var encodedLengthBytes = BitConverter.GetBytes((long)encodedLength);

                    writer.Write(encodedLengthBytes, 0, Convert.ToInt32(encodedLengthBytes.GetLength(0)));
                    writer.Write(encodedBuffer, 0, encodedLength);

                    bytesToSend -= readCount;
                }
            }
            return true;
        }

        public static async Task<bool> ReceiveCompressedFileAsync(this Stream reader, String path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new FileStream(path, FileMode.Create))
            {
                var encodedBuffer = new byte[BUFFER_SIZE];
                var decodedBuffer = new byte[encodedBuffer.Length * 255];

                var fileSizeBytes = new byte[sizeof(long)];
                reader.Read(fileSizeBytes, 0, Convert.ToInt32(fileSizeBytes.Length));
                var bytesToReceive = BitConverter.ToInt64(fileSizeBytes, 0);

                var receiveBlockSize = new byte[sizeof(long)];

                while (bytesToReceive > 0)
                {
                    reader.Read(receiveBlockSize, 0, Convert.ToInt32(receiveBlockSize.Length));
                    var blockSize = BitConverter.ToInt64(receiveBlockSize, 0);

                    var receivedBytes = reader.Read(encodedBuffer, 0, Convert.ToInt32(blockSize));
                    if (receivedBytes != blockSize) throw new IOException("ReceiveFileAsync network read error");

                    var decodedBytes = LZ4Codec.Decode(encodedBuffer, 0, receivedBytes, decodedBuffer, 0, decodedBuffer.Length);
                    writer.Write(decodedBuffer, 0, Convert.ToInt32(decodedBytes));
                    bytesToReceive -= decodedBytes;
                }
            }
            return true;
        }



        public static async Task<bool> ReceiveFileAsync(this Stream reader, String path, bool useCompression)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new FileStream(path, FileMode.Create))
            {
                var buffer = new byte[BUFFER_SIZE];

                var fileSizeBytes = new byte[sizeof(long)];
                reader.Read(fileSizeBytes, 0, Convert.ToInt32(fileSizeBytes.Length));
                var bytesToReceive = BitConverter.ToInt64(fileSizeBytes, 0);

                while (bytesToReceive > 0)
                {
                    var receiveSize = Convert.ToInt32(Math.Min(bytesToReceive, BUFFER_SIZE));

                    var receivedBytes = reader.Read(buffer, 0, Convert.ToInt32(receiveSize));
                    if (receivedBytes != receiveSize) throw new IOException("ReceiveFileAsync network read error");

                    writer.Write(buffer, 0, Convert.ToInt32(receivedBytes));
                    bytesToReceive -= receivedBytes;                  
                }
            }
            return true;
        }
    }
}
