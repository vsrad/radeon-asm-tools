using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Deborgar;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.ProjectSystem
{
    public class DebuggerIntegrationTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            TestHelper.InitializePackageTaskFactory();

            /* Create a test project */

            var projectMock = new Mock<IProject>();
            var options = new ProjectOptions();
            options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            projectMock.Setup((p) => p.Options).Returns(options);
            var project = projectMock.Object;

            project.Options.Profile.MenuCommands.DebugAction = "Debug";
            project.Options.Profile.General.CopySources = false;
            project.Options.Profile.General.DeployDirectory = "";
            project.Options.Profile.General.LocalWorkDir = "local/dir";
            project.Options.Profile.General.RemoteWorkDir = "/periphery/votw";
            project.Options.Profile.Actions.Add(new ActionProfileOptions { Name = "Debug" });
            project.Options.DebuggerOptions.Watches.Add(new Watch("a", VariableType.Hex, false));
            project.Options.DebuggerOptions.Watches.Add(new Watch("c", VariableType.Hex, false));
            project.Options.DebuggerOptions.Watches.Add(new Watch("tide", VariableType.Hex, false));

            var readDebugDataStep = new ReadDebugDataStep { BinaryOutput = false, OutputOffset = 1 };
            readDebugDataStep.OutputFile.CheckTimestamp = true;
            readDebugDataStep.OutputFile.Path = "output-path";

            project.Options.Profile.Actions[0].Steps.Add(new ExecuteStep
                { Executable = "ohmu", Arguments = "-break-line $(RadBreakLine) -source $(RadActiveSourceFile) -source-line $(RadActiveSourceFileLine) -watch $(RadWatches)" });
            project.Options.Profile.Actions[0].Steps.Add(readDebugDataStep);

            var codeEditor = new Mock<IActiveCodeEditor>();
            codeEditor.Setup(e => e.GetCurrentLine()).Returns(13);
            var breakpointTracker = new Mock<IBreakpointTracker>();
            breakpointTracker.Setup(t => t.MoveToNextBreakTarget(false)).Returns((@"C:\MEHVE\JATO.s", new[] { 666u }));

            var serviceProvider = new Mock<SVsServiceProvider>();
            serviceProvider.Setup(p => p.GetService(typeof(SVsStatusbar))).Returns(new Mock<IVsStatusbar>().Object);

            var channel = new MockCommunicationChannel();
            var actionLauncher = new ActionLauncher(project, new Mock<IActionLogger>().Object, channel.Object, new Mock<IFileSynchronizationManager>().Object,
                codeEditor.Object, breakpointTracker.Object, serviceProvider.Object);
            var debuggerIntegration = new DebuggerIntegration(project, actionLauncher, codeEditor.Object);

            /* Set up server responses */

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata timestampFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "output-path" }, timestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (Execute execute) =>
            {
                Assert.Equal("ohmu", execute.Executable);
                Assert.Equal(@"-break-line 666 -source JATO.s -source-line 13 -watch a:c:tide", execute.Arguments);
            });
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now });

            /* Start debugging */

            var tcs = new TaskCompletionSource<ExecutionCompletedEventArgs>();
            BreakState breakState = null;

            debuggerIntegration.ExecutionCompleted += (s, e) => tcs.SetResult(e);
            debuggerIntegration.BreakEntered += (s, e) => breakState = e;

            var engine = debuggerIntegration.RegisterEngine();
            engine.Execute(false);

            var execCompletedEvent = await tcs.Task;

            Assert.NotNull(execCompletedEvent);
            Assert.Equal(@"C:\MEHVE\JATO.s", execCompletedEvent.File);
            Assert.Equal(666u, execCompletedEvent.Lines[0]);

            Assert.NotNull(breakState);
            Assert.Equal(3, breakState.Data.Watches.Count);
            Assert.Equal("a", breakState.Data.Watches[0]);
            Assert.Equal("c", breakState.Data.Watches[1]);
            Assert.Equal("tide", breakState.Data.Watches[2]);
        }
    }
}
