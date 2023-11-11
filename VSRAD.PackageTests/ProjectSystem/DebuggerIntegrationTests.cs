using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Deborgar;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.ProjectSystem
{
    [Collection(MockedVS.Collection)]
    public class DebuggerIntegrationTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();

            /* Create a test project */

            var projectMock = new Mock<IProject>();
            var options = new ProjectOptions();
            options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            projectMock.Setup((p) => p.Options).Returns(options);
            var breakLineTagger = new Mock<BreakLineGlyphTaggerProvider>();
            projectMock.Setup((p) => p.GetExportByMetadataAndType(It.IsAny<Predicate<IAppliesToMetadataView>>(), It.IsAny<Predicate<IViewTaggerProvider>>()))
                .Returns(breakLineTagger.Object);
            var project = projectMock.Object;
            project.Options.Profile.MenuCommands.DebugAction = "Debug";
            project.Options.Profile.General.LocalWorkDir = "local/dir";
            project.Options.Profile.General.RemoteWorkDir = "/periphery/votw";
            project.Options.Profile.Actions.Add(new ActionProfileOptions { Name = "Debug" });
            project.Options.DebuggerOptions.EnableMultipleBreakpoints = true;
            project.Options.DebuggerOptions.Watches.AddRange(TestHelper.ReadFixtureLines("Watches.txt").Select(w => new Watch(w, new VariableType(VariableCategory.Hex, 32))));

            var readDebugDataStep = new ReadDebugDataStep { BinaryOutput = true, OutputOffset = 0 };
            readDebugDataStep.OutputFile.CheckTimestamp = false;
            readDebugDataStep.OutputFile.Path = TestHelper.GetFixturePath("DebugBuffer.bin");
            readDebugDataStep.OutputFile.Location = StepEnvironment.Local;
            readDebugDataStep.WatchesFile.CheckTimestamp = true;
            readDebugDataStep.WatchesFile.Path = "watches-path";
            readDebugDataStep.DispatchParamsFile.CheckTimestamp = false;
            readDebugDataStep.DispatchParamsFile.Path = "dispatch-params-path";

            project.Options.Profile.Actions[0].Steps.Add(new ExecuteStep
            { Executable = "ohmu", Arguments = "-source $(RadActiveSourceFile) -source-line $(RadActiveSourceFileLine)" });
            project.Options.Profile.Actions[0].Steps.Add(readDebugDataStep);

            var activeEditor = new Mock<IEditorView>();
            activeEditor.Setup(e => e.GetFilePath()).Returns(@"C:\MEHVE\JATO.s");
            activeEditor.Setup(e => e.GetCaretPos()).Returns((13, 0));
            var sourceManager = new Mock<IProjectSourceManager>();
            sourceManager.Setup(m => m.GetActiveEditorView()).Returns(activeEditor.Object);
            var breakpointTracker = new Mock<IBreakpointTracker>();
            breakpointTracker.Setup(t => t.GetTarget(@"C:\MEHVE\JATO.s", BreakTargetSelector.Multiple))
                .Returns(new BreakTarget(new[] { new BreakpointInfo(@"C:\MEHVE\JATO.s", 25u, 1, false), new BreakpointInfo(@"C:\MEHVE\JATO.s", 31u, 1, false) }, BreakTargetSelector.Multiple, "", 0, ""));

            var serviceProvider = new Mock<SVsServiceProvider>();
            serviceProvider.Setup(p => p.GetService(typeof(SVsStatusbar))).Returns(new Mock<IVsStatusbar>().Object);

            var channel = new MockCommunicationChannel();
            var actionLauncher = new ActionLauncher(project, channel.Object, sourceManager.Object,
                breakpointTracker.Object, serviceProvider.Object);
            var debuggerIntegration = new DebuggerIntegration(project, actionLauncher, new Mock<IActionLogger>().Object, breakpointTracker.Object, sourceManager.Object);

            /* Set up server responses */

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata timestampFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "watches-path" }, timestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (Execute execute) =>
            {
                Assert.Equal("ohmu", execute.Executable);
                Assert.Equal(@"-source JATO.s -source-line 13", execute.Arguments);
            });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100), Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") }, (FetchResultRange watchesFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "watches-path" }, watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") }, (FetchResultRange dispatchParamsFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "dispatch-params-path" }, dispatchParamsFetch.FilePath));

            /* Start debugging */

            var tcs = new TaskCompletionSource<ExecutionCompletedEventArgs>();
            Result<BreakState> breakResult = null;

            debuggerIntegration.ExecutionCompleted += (s, e) => tcs.SetResult(e);
            debuggerIntegration.BreakEntered += (s, e) => breakResult = e;

            var engine = debuggerIntegration.RegisterEngine();
            engine.Execute(false);

            var execCompletedEvent = await tcs.Task;

            Assert.True(execCompletedEvent.IsSuccessful);
            Assert.Empty(packageErrors);
            Assert.NotNull(execCompletedEvent);
            Assert.Collection(execCompletedEvent.BreakLocations,
                (i0) => Assert.Equal((@"C:\MEHVE\JATO.s", 25u), (i0.CallStack[0].SourcePath, i0.CallStack[0].SourceLine)),
                (i1) => Assert.Equal((@"C:\MEHVE\JATO.s", 31u), (i1.CallStack[0].SourcePath, i1.CallStack[0].SourceLine)));

            sourceManager.Verify(s => s.SaveProjectState(), Times.Once);

            Assert.True(breakResult.TryGetResult(out var breakState, out _));
            Assert.Equal(16384u, breakState.DispatchParameters.GridSizeX);
            Assert.Equal(512u, breakState.DispatchParameters.GroupSizeX);
            Assert.Equal(64u, breakState.DispatchParameters.WaveSize);
            Assert.Equal(new[] { "tid", "lst", "a", "c", "tide", "lst[1]" }, breakState.Data.Watches.Keys);

            breakLineTagger.Verify(t => t.OnExecutionCompleted(sourceManager.Object, execCompletedEvent));
        }
    }
}
