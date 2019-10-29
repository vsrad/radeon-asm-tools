using Microsoft.VisualStudio.Shell;
using Moq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using VSGCN.DebugServer.IPC.Commands;
using VSGCN.DebugServer.IPC.Responses;
using VSGCN.Package.ProjectSystem.Macros;
using VSGCN.Package.Server;
using VSGCN.PackageTests;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSGCN.Package.ProjectSystem.Tests
{
    [Collection("Sequential")]
    public class DebugSessionTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = CreateProject(workingDirectory: "/remote/dir", outputPath: "file");

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/remote/dir", "file" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now },
                (command) => Assert.Equal(new[] { "/remote/dir", "file" }, command.FilePath));

            var result = await session.ExecuteToLineAsync(13, new ReadOnlyCollection<string>(new[] { "one", "two" }.ToList()));
            Assert.True(result.TryGetResult(out var breakState, out _));
            Assert.Collection(breakState.Watches, (first) => Assert.Equal("one", first), (second) => Assert.Equal("two", second));

            Assert.True(channel.AllInteractionsHandled);
        }

        [Fact]
        public async Task NonZeroExitCodeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = CreateProject(workingDirectory: "/remote/dir", outputPath: "file");

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/remote/dir", "file" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 33 }, (_) => { });

            var result = await session.ExecuteToLineAsync(13, new ReadOnlyCollection<string>(new[] { "one", "two" }.ToList()));
            Assert.False(result.TryGetResult(out _, out var error));
            Assert.Equal(RemoteCommandExecutor.ErrorNonZeroExitCode(33), error.Message);

            Assert.True(channel.AllInteractionsHandled);
        }

        private static IProject CreateProject(string workingDirectory, string outputPath)
        {
            var mock = new Mock<IProject>(MockBehavior.Strict);
            var options = new Options.ProjectOptions();
            options.AddProfile("Default", new Options.ProfileOptions());
            mock.Setup((p) => p.Options).Returns(options);

            var macros = new Mock<IMacroEvaluator>();
            macros.Setup((e) => e.GetMacroValueAsync(GcnMacros.DebuggerWorkingDirectory)).Returns(Task.FromResult(workingDirectory));
            macros.Setup((e) => e.GetMacroValueAsync(GcnMacros.DebuggerOutputPath)).Returns(Task.FromResult(outputPath));

            mock.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(macros.Object));
            return mock.Object;
        }
    }
}
