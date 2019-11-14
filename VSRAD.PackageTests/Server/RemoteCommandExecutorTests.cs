using Moq;
using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class RemoteCommandExecutorTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (outputWindow, outputWriterMock) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "file", "path" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted
            {
                Status = ExecutionStatus.Completed,
                ExitCode = 0,
                Stdout = "test stdout",
                Stderr = "test stderr"
            },
            (command) =>
            {
                Assert.Equal("exec", command.Executable);
                Assert.Equal("args", command.Arguments);
            });
            var data = new byte[] { 0xca, 0xfe, 0xde, 0xad };
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, Data = data },
                (command) => Assert.Equal(new[] { "file", "path" }, command.FilePath));

            var result = await executor.ExecuteWithResultAsync(new Execute { Executable = "exec", Arguments = "args" }, new OutputFile("file", "path"));
            Assert.True(result.TryGetResult(out var resultData, out _));
            Assert.Equal(data, resultData.Item2);
            Assert.Equal("test stdout", resultData.Item1.Stdout);

            outputWriterMock.Verify((w) => w.PrintMessageAsync(
                "[Test] Captured stdout", "test stdout"), Times.Once);
            outputWriterMock.Verify((w) => w.PrintMessageAsync(
                "[Test] Captured stderr", "test stderr"), Times.Once);
        }

        [Fact]
        public async Task ExecutionErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (outputWindow, outputWriterMock) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch });

            var result = await executor.ExecuteWithResultAsync(new Execute(), new OutputFile("", "h"));
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal("RAD Test", error.Title);
            Assert.Equal(RemoteCommandExecutor.ErrorCouldNotLaunch, error.Message);
            outputWriterMock.Verify((w) => w.PrintMessageAsync("[Test] No stdout/stderr captured", null), Times.Once);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 666 });
            result = await executor.ExecuteWithResultAsync(new Execute(), new OutputFile("", "h"));
            Assert.False(result.TryGetResult(out _, out error));
            Assert.Equal(RemoteCommandExecutor.ErrorNonZeroExitCode(666), error.Message);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut });
            result = await executor.ExecuteWithResultAsync(new Execute(), new OutputFile("", "h"));
            Assert.False(result.TryGetResult(out _, out error));
            Assert.Equal(RemoteCommandExecutor.ErrorTimedOut, error.Message);

            Assert.True(channel.AllInteractionsHandled);
        }

        [Fact]
        public async Task FetchResultTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (outputWindow, _) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound });
            var result = await executor.ExecuteWithResultAsync(new Execute(), new OutputFile(@"F:\Is\Pressed\For", "Us"));
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal(RemoteCommandExecutor.ErrorFileNotCreated, error.Message);

            var timestamp = DateTime.Now;
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = timestamp });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = timestamp });
            result = await executor.ExecuteWithResultAsync(new Execute(), new OutputFile(@"F:\Is\Pressed\For", "Us"));
            Assert.False(result.TryGetResult(out _, out error));
            Assert.Equal(RemoteCommandExecutor.ErrorFileUnchanged, error.Message);

            Assert.True(channel.AllInteractionsHandled);
        }

        private static (IOutputWindowManager, Mock<IOutputWindowWriter>) MockOutputWindow()
        {
            var outputWriter = new Mock<IOutputWindowWriter>();
            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((m) => m.GetExecutionResultPane()).Returns(outputWriter.Object);
            return (outputWindow.Object, outputWriter);
        }
    }
}
