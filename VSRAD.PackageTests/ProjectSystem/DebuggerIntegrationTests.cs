using Microsoft.VisualStudio.Shell;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public class DebuggerIntegrationTests
    {
        [Fact]
        public async Task DebuggerLaunchTestAsync()
        {
            var channel = new MockCommunicationChannel();
            TestHelper.InitializePackageTaskFactory();
            TestHelper.SetupGlobalErrorMessageSink();

            var serviceProvider = new Mock<SVsServiceProvider>();
            var outputWindow = new Mock<IOutputWindowManager>();
            outputWindow.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            var codeEditor = new Mock<IActiveCodeEditor>();
            var deployManager = new Mock<IFileSynchronizationManager>();

            var debugger = new DebuggerIntegration(serviceProvider.Object, codeEditor.Object,
                deployManager.Object, outputWindow.Object, channel.Object);

            debugger.SetProjectOnLoad(CreateProject(watches: new[] { "watch1" },
                workingDirectory: "/remote/dir", outputPath: "file")); /* Performed by ProjectLifecycle */

            /* Performed by DebuggerLaunchProvider */
            // FIXME
            //debugger.CreateDebugSession();

            var session = debugger.RegisterEngine();
            var completedTcs = new TaskCompletionSource<bool>();
            session.ExecutionCompleted += (_) => completedTcs.SetResult(true);

            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound },
                (command) => Assert.Equal(new[] { "/remote/dir", "file" }, command.FilePath));
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch }, (_) => { });

            session.ExecuteToLine(7);

            Assert.True(await completedTcs.Task);
            Assert.True(channel.AllInteractionsHandled);
        }

        private static IProject CreateProject(IEnumerable<string> watches, string workingDirectory, string outputPath)
        {
            var mock = new Mock<IProject>(MockBehavior.Strict);
            var options = new Options.ProjectOptions();
            options.AddProfile("Default", new Options.ProfileOptions());
            foreach (var watch in watches)
                options.DebuggerOptions.Watches.Add(new DebugVisualizer.Watch(watch, DebugVisualizer.VariableType.Hex, false));
            mock.Setup((p) => p.Options).Returns(options);

            var macros = new Mock<IMacroEvaluator>();
            macros.Setup((e) => e.GetMacroValueAsync(RadMacros.DebuggerWorkingDirectory)).Returns(Task.FromResult(workingDirectory));
            macros.Setup((e) => e.GetMacroValueAsync(RadMacros.DebuggerOutputPath)).Returns(Task.FromResult(outputPath));

            mock.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(macros.Object));
            return mock.Object;
        }
    }
}
