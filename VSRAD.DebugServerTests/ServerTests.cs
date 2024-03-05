using System.IO;
using System.Net;
using System.Net.Sockets;
using VSRAD.DebugServer;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests
{
    public class ServerTests
    {
        [Fact]
        public async void ExecuteTestAsync()
        {
            var server = new Server(IPAddress.Any, 13333, new DebugServer.Logging.GlobalLogger());
            _ = server.LoopAsync();

            var tmpFile = Path.GetTempFileName();
            var tmpDirectory = Path.GetDirectoryName(tmpFile);
            var tmpFileRelative = Path.GetFileName(tmpFile);

            var command = new DebugServer.IPC.Commands.Execute
            {
                WorkingDirectory = tmpDirectory,
                Executable = "python.exe",
                Arguments = $"-c \"print('command ran successfully',  file=open('{tmpFileRelative}', 'w'))\"",
                RunAsAdministrator = false,
                ExecutionTimeoutSecs = 0
            };
            using (var client = new TcpClient("127.0.0.1", 13333).GetStream())
            {
                await client.WriteSerializedMessageAsync(command);
                var (response, _) = await client.ReadSerializedResponseAsync<ExecutionCompleted>();
                Assert.Equal(ExecutionStatus.Completed, response.Status);
            }
            var tmpContents = File.ReadAllText(tmpFile);
            Assert.Equal("command ran successfully\r\n", tmpContents);
        }

        [Fact]
        public async void SequentialCommandProcessingTest()
        {
            var server = new Server(IPAddress.Any, 13337, new DebugServer.Logging.GlobalLogger());
            _ = server.LoopAsync();

            using var client1 = new TcpClient("127.0.0.1", 13337);
            using var client2 = new TcpClient("127.0.0.1", 13337);
            using var client3 = new TcpClient("127.0.0.1", 13337);
            using var stream1 = client1.GetStream();
            using var stream2 = client2.GetStream();
            using var stream3 = client3.GetStream();

            await stream1.WriteSerializedMessageAsync(new DebugServer.IPC.Commands.Execute
            {
                Executable = "python.exe",
                Arguments = $"-c \"from time import sleep; sleep(0.2); print('h')"
            }).ConfigureAwait(false);
            // This command should finish executing faster than the first one, but its result should arrive later
            // because commands are executed sequentially
            await stream1.WriteSerializedMessageAsync(new DebugServer.IPC.Commands.FetchMetadata
            {
                FilePath = new[] { @"N:\owhere" }
            }).ConfigureAwait(false);

            var (response1, _) = await stream1.ReadSerializedResponseAsync<ExecutionCompleted>().ConfigureAwait(false);
            Assert.Equal(ExecutionStatus.Completed, response1.Status);
            Assert.Equal("h\r\n", response1.Stdout);

            var (response2, _) = await stream1.ReadSerializedResponseAsync<MetadataFetched>().ConfigureAwait(false);
            Assert.Equal(FetchStatus.FileNotFound, response2.Status);

            // This command contains invalid archive data and should trigger an exception;
            // however, the global execution lock should be released so subsequent commands can be processed.
            await stream1.WriteSerializedMessageAsync(new DebugServer.IPC.Commands.Deploy
            {
                Destination = "the wired",
                Data = new byte[] { 0xBE, 0xEF } // invalid archive data should trigger an exception
            }).ConfigureAwait(false);
            await stream2.WriteSerializedMessageAsync(new DebugServer.IPC.Commands.FetchResultRange
            {
                FilePath = new[] { @"H:\what" }
            }).ConfigureAwait(false);
            var (responseAfterException, _) = await stream2.ReadSerializedResponseAsync<ResultRangeFetched>();
            Assert.Equal(FetchStatus.FileNotFound, responseAfterException.Status);

            await stream2.WriteAsync(new byte[] { 0, 0, 0, 0 }); // invalid command should not affect other clients
            await stream3.WriteSerializedMessageAsync(new DebugServer.IPC.Commands.Execute()).ConfigureAwait(false);
            var (response, _) = await stream3.ReadSerializedResponseAsync<ExecutionCompleted>().ConfigureAwait(false);
            Assert.Equal(ExecutionStatus.CouldNotLaunch, response.Status);
        }
    }
}
