using Moq;
using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class RemoteCommandExecutorTests
    {
        [Fact]
        public async Task ExecuteRemoteTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (outputWindow, outputWriterMock) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

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

            await executor.ExecuteRemoteAsync(new Execute { Executable = "exec", Arguments = "args" });
            outputWriterMock.Verify((w) => w.PrintMessageAsync(
                "[Test] Captured stdout", "test stdout"), Times.Once);
            outputWriterMock.Verify((w) => w.PrintMessageAsync(
                "[Test] Captured stderr", "test stderr"), Times.Once);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch });
            var exception = await Assert.ThrowsAsync<RemoteCommandExecutor.ExecutionFailedException>(() =>
                executor.ExecuteRemoteAsync(new Execute()));
            Assert.Equal(RemoteCommandExecutor.ErrorCouldNotLaunch, exception.Message);
            outputWriterMock.Verify((w) => w.PrintMessageAsync("[Test] No stdout/stderr captured", null), Times.Once);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 666 });
            exception = await Assert.ThrowsAsync<RemoteCommandExecutor.ExecutionFailedException>(() =>
                executor.ExecuteRemoteAsync(new Execute()));
            Assert.Equal(RemoteCommandExecutor.ErrorNonZeroExitCode(666), exception.Message);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut });
            exception = await Assert.ThrowsAsync<RemoteCommandExecutor.ExecutionFailedException>(() =>
                executor.ExecuteRemoteAsync(new Execute()));
            Assert.Equal(RemoteCommandExecutor.ErrorTimedOut, exception.Message);

            Assert.True(channel.AllInteractionsHandled);
        }

        [Fact]
        public async Task GetMetadataTestAsync()
        {
            var time = DateTime.MaxValue;

            var channel = new MockCommunicationChannel();
            var (outputWindow, outputWriterMock) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched
            {
                ByteCount = 1101,
                Status = FetchStatus.Successful,
                Timestamp = time
            },
            (command) =>
            {
                Assert.Equal(new[] { @"WHO:\CARES" }, command.FilePath);
                Assert.True(command.BinaryOutput);
            });

            var (actualTime, dwordCount) = (await executor.FetchMetadataAsync(new[] { @"WHO:\CARES" }, true)).Value;
            Assert.Equal(actualTime, time);
            Assert.Equal(1101, dwordCount);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            Assert.Null(await executor.FetchMetadataAsync(new[] { "nonexistent" }, true));

            Assert.True(channel.AllInteractionsHandled);
        }

        [Fact]
        public async Task FetchResultTestAsync()
        {
            var time = DateTime.MaxValue;

            var channel = new MockCommunicationChannel();
            var (outputWindow, outputWriterMock) = MockOutputWindow();
            var executor = new RemoteCommandExecutor("Test", channel.Object, outputWindow);

            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched
            {
                Data = new byte[] { 11, 01, 11, 10 },
                Status = FetchStatus.Successful,
                Timestamp = DateTime.MaxValue
            },
            (command) =>
            {
                Assert.Equal(new[] { @"F:\Is\Pressed\For", "Us" }, command.FilePath);
                Assert.Equal(4, command.ByteCount);
                Assert.True(command.BinaryOutput);
                Assert.Equal(0, command.ByteOffset);
            });

            var (actualTime, data) = await executor.FetchResultAsync(new[] { @"F:\Is\Pressed\For", "Us" }, true, 4);
            Assert.Equal(time, actualTime);
            Assert.Equal(new byte[] { 11, 01, 11, 10 }, data);

            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound });
            var exception = await Assert.ThrowsAsync<RemoteCommandExecutor.ExecutionFailedException>(() =>
                executor.FetchResultAsync(new[] { "nonexistent" }, true, 4));
            Assert.Equal(RemoteCommandExecutor.ErrorFileNotCreated, exception.Message);

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
