using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.PackageTests;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem.Tests
{
    [Collection("Sequential")]
    public class DebugSessionTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile(new Dictionary<string, string>()
            {
                { RadMacros.DebuggerWorkingDirectory, "/glitch/city" },
                { RadMacros.DebuggerOutputPath, "va11" }
            }).Object;

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var errorListManager = new Mock<IErrorListManager>();
            errorListManager.Setup((m) => m.AddToErrorListAsync("")).Returns(Task.CompletedTask);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object, errorListManager.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));

            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "jill", "julianne" }.ToList()));
            Assert.True(result.TryGetResult(out var breakState, out _));
            Assert.Collection(breakState.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));

            Assert.True(channel.AllInteractionsHandled);
        }

        [Fact]
        public async Task NonZeroExitCodeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile(new Dictionary<string, string>()
            {
                { RadMacros.DebuggerWorkingDirectory, "/glitch/city" },
                { RadMacros.DebuggerOutputPath, "va11" }
            }).Object;

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var errorListManager = new Mock<IErrorListManager>();
            errorListManager.Setup((m) => m.AddToErrorListAsync("")).Returns(Task.CompletedTask);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object, errorListManager.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 33 }, (_) => { });

            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "jill", "julianne" }.ToList()));
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal("Debugger command on the target machine returned a non-zero exit code (33). Check your application or debug script output in Output -> RAD Debug.", error.Message);

            Assert.True(channel.AllInteractionsHandled);
        }
    }
}