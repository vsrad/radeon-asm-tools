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
    }
}
