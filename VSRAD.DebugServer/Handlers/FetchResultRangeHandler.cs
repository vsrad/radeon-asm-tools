using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

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

            var timestamp = File.GetLastWriteTime(_filePath).ToUniversalTime();
            var data = _command.BinaryOutput
                ? await ParseDebuggerOutputBinaryAsync()
                : await ParseDebuggerOutputTextAsync();

            return new ResultRangeFetched { Status = FetchStatus.Successful, Data = data, Timestamp = timestamp };
        }

        internal async Task<byte[]> ParseDebuggerOutputBinaryAsync()
        {
            if (_command.ByteOffset == 0 && _command.ByteCount == 0)
                return await File.ReadAllBytesAsync(_filePath);

            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan);

            byte[] buffer = new byte[_command.ByteCount];
            stream.Seek(_command.ByteOffset + _command.OutputOffset, SeekOrigin.Begin);
            int read = await stream.ReadAsync(buffer, 0, _command.ByteCount);
            byte[] data = new byte[read];
            Array.Copy(buffer, data, read);
            return data;
        }

        internal async Task<byte[]> ParseDebuggerOutputTextAsync()
        {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            return await TextDebuggerOutputParser.ReadTextOutputAsync(stream, _command.OutputOffset, _command.ByteOffset, _command.ByteCount);
        }
    }
}
