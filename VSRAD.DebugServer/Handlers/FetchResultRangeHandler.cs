using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class FetchResultRangeHandler : IHandler
    {
        private readonly IPC.Commands.FetchResultRange _command;
        private readonly string _filePath;
        private const int _bufferSize = 8192;

        public FetchResultRangeHandler(IPC.Commands.FetchResultRange command)
        {
            _command = command;
            _filePath = Path.Combine(command.FilePath);
        }

        public async Task<IResponse> RunAsync()
        {
            if (!File.Exists(_filePath))
            {
                return new ResultRangeFetched { Status = FetchStatus.FileNotFound };
            }

            var timestamp = File.GetLastWriteTimeUtc(_filePath);
            var data = _command.BinaryOutput
                ? await ParseDebuggerOutputBinaryAsync()
                : await ParseDebuggerOutputTextAsync();

            return new ResultRangeFetched { Status = FetchStatus.Successful, Data = data, Timestamp = timestamp };
        }

        internal async Task<byte[]> ParseDebuggerOutputBinaryAsync()
        {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            stream.Seek(_command.ByteOffset + _command.OutputOffset, SeekOrigin.Begin);

            var bytesAvailable = Math.Max(0, (int)(stream.Length - stream.Position));
            var bytesToRead = _command.ByteCount == 0 ? bytesAvailable : Math.Min(_command.ByteCount, bytesAvailable);
            byte[] buffer = new byte[bytesToRead];

            int read = 0, bytesRead = 0;
            while (bytesRead != bytesToRead)
            {
                if ((read = await stream.ReadAsync(buffer, 0, bytesToRead - bytesRead)) == 0)
                    throw new IOException("Output file length does not match stream length");
                bytesRead += read;
            }
            return buffer;
        }

        internal async Task<byte[]> ParseDebuggerOutputTextAsync()
        {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            return await TextDebuggerOutputParser.ReadTextOutputAsync(stream, _command.OutputOffset, _command.ByteOffset, _command.ByteCount);
        }
    }
}
