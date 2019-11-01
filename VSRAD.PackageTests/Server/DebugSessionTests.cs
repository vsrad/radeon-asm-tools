using Moq;
using System;
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
            var project = CreateProject(workingDirectory: "/glitch/city", outputPath: "va11");

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));

            var result = await session.ExecuteToLineAsync(13, new ReadOnlyCollection<string>(new[] { "jill", "julianne" }.ToList()));
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
            var project = CreateProject(workingDirectory: "/glitch/city", outputPath: "va11");

            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, outputWindow.Object);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/glitch/city", "va11" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 33 }, (_) => { });

            var result = await session.ExecuteToLineAsync(13, new ReadOnlyCollection<string>(new[] { "jill", "julianne" }.ToList()));
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
            macros.Setup((e) => e.GetMacroValueAsync(RadMacros.DebuggerWorkingDirectory)).Returns(Task.FromResult(workingDirectory));
            macros.Setup((e) => e.GetMacroValueAsync(RadMacros.DebuggerOutputPath)).Returns(Task.FromResult(outputPath));

            mock.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(macros.Object));
            return mock.Object;
        }
    }
}