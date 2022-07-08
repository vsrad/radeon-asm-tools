using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public static class StreamExtensions
    {
        // Since every command and response contains at least one byte (message type), zero-length messages are treated as pings
        private static readonly byte[] _pingMessage = new byte[] { 0, 0, 0, 0 };

        public static async Task<(T, int)> ReadSerializedCommandAsync<T>(this Stream stream, HashSet<ExtensionCapability> extensionCapabilities) where T : ICommand
        {
            while (true) // Loop to reply to pings (sent in case of long-running commands)
            {
                byte[] messageLengthBytes = new byte[4];
                if (await stream.ReadAsync(messageLengthBytes, 0, 4).ConfigureAwait(false) != 4)
                    throw new EndOfStreamException();

                int messageLength = BitConverter.ToInt32(messageLengthBytes, 0);
                if (messageLength == 0) // Reply to a ping with a pong
                {
                    await stream.WriteAsync(_pingMessage, 0, _pingMessage.Length);
                    continue;
                }

                byte[] messageBytes = new byte[messageLength];

                int buffered = 0;
                while (buffered != messageLength)
                {
                    var received = await stream.ReadAsync(messageBytes, buffered, messageLength - buffered).ConfigureAwait(false);
                    buffered += received;
                    if (buffered != messageLength && received == 0)
                        throw new EndOfStreamException();
                }
                using (var memStream = new MemoryStream(messageBytes))
                using (var reader = new IPCReader(memStream))
                    return ((T)reader.ReadCommand(extensionCapabilities), messageLength);
            }
        }

        public static async Task<(T, int)> ReadSerializedResponseAsync<T>(this Stream stream) where T : IResponse
        {
            while (true) // Loop to reply to pings (sent in case of long-running commands)
            {
                byte[] messageLengthBytes = new byte[4];
                if (await stream.ReadAsync(messageLengthBytes, 0, 4).ConfigureAwait(false) != 4)
                    throw new EndOfStreamException();

                int messageLength = BitConverter.ToInt32(messageLengthBytes, 0);
                if (messageLength == 0) // Reply to a ping with a pong
                {
                    await stream.WriteAsync(_pingMessage, 0, _pingMessage.Length);
                    continue;
                }

                byte[] messageBytes = new byte[messageLength];

                int buffered = 0;
                while (buffered != messageLength)
                {
                    var received = await stream.ReadAsync(messageBytes, buffered, messageLength - buffered).ConfigureAwait(false);
                    buffered += received;
                    if (buffered != messageLength && received == 0)
                        throw new EndOfStreamException();
                }
                using (var memStream = new MemoryStream(messageBytes))
                using (var reader = new IPCReader(memStream))
                    return ((T)reader.ReadResponse(), messageLength);
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

        /// <summary>
        /// Send a ping to the remote host and wait for a pong reply. <b>May corrupt the stream</b> if:
        /// <list type="bullet">
        /// <item>The receiver does not support pings: ensure that the receiver has the Base capability</item>
        /// <item>The receiver is expected to send data when you ping it: don't send pings from the server unless
        /// the extension is waiting for a response and don't send pings from the extension while waiting for a response</item>
        /// </list>
        /// </summary>
        /// <exception cref="EndOfStreamException">Thrown when the remote host does not reply to the ping</exception>
        public static async Task PingUnsafeAsync(this Stream stream)
        {
            try
            {
                using (var cts = new CancellationTokenSource(millisecondsDelay: 2000))
                using (cts.Token.Register(() => stream.Close()))
                {
                    await stream.WriteAsync(_pingMessage, 0, _pingMessage.Length).ConfigureAwait(false);
                    byte[] pongBytes = new byte[_pingMessage.Length];
                    if (await stream.ReadAsync(pongBytes, 0, pongBytes.Length).ConfigureAwait(false) != pongBytes.Length)
                        throw new EndOfStreamException();
                }
            }
            catch
            {
                throw new EndOfStreamException();
            }
        }
    }
}
