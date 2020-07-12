using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.Server
{
    [Collection("Sequential")]
    public class DebugSessionTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile(remoteWorkDir: "/remote/workdir").Object;
            project.Options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            project.Options.Profile.Debugger.Steps.Add(new ExecuteStep { Executable = "va11" });
            project.Options.Profile.Debugger.OutputFile.CheckTimestamp = true;
            project.Options.Profile.Debugger.OutputFile.Path = "/glitch/city";
            project.Options.Profile.Debugger.WatchesFile.CheckTimestamp = false;
            project.Options.Profile.Debugger.WatchesFile.Path = "/glitch/city/bar";

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound } }, (commands) =>
            {
                var initTimestampFetch = (FetchMetadata)commands[0];
                Assert.Equal(new[] { "/remote/workdir", "/glitch/city" }, initTimestampFetch.FilePath);
            });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new IResponse[]
            {
                new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("jill\njulianne") }, // valid watches
                new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now } // output
            });

            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, null);
            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "invalid", "watches" }.ToList()));
            Assert.True(channel.AllInteractionsHandled);
            Assert.Null(result.Error);
            var breakState = result.BreakState;
            Assert.Collection(breakState.Data.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));
        }

        [Fact]
        public async Task NonZeroExitCodeTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile().Object;
            project.Options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            project.Options.Profile.Debugger.Steps.Add(new ExecuteStep { Executable = "va11" });
            project.Options.Profile.Debugger.OutputFile.CheckTimestamp = true;
            project.Options.Profile.Debugger.OutputFile.Path = "/glitch/city";

            // Init timestamp fetch (output file)
            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound } });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 33 });
            channel.ThenRespond(new IResponse[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now } });

            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, null);
            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "jill", "julianne" }.ToList()));
            Assert.True(channel.AllInteractionsHandled);
            Assert.Null(result.Error);
            Assert.Equal("va11 process exited with a non-zero code (33). Check your application or debug script output in Output -> RAD Debug.", result.ActionResult.StepResults[0].Warning);
        }

        [Fact]
        public async Task ConfigValidationTestAsync()
        {
            var project = TestHelper.MakeProjectWithProfile().Object;
            project.Options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            project.Options.Profile.Debugger.OutputFile.Path = "";
            var session = new DebugSession(project, null, null, null);
            var result = await session.ExecuteAsync(new[] { 13u }, null);
            Assert.Equal("Debugger output path is not specified. To set it, go to Tools -> RAD Debug -> Options and edit your current profile.", result.Error.Value.Message);

            project.Options.Profile.Debugger.OutputFile.Path = "C:\\Users\\J\\output";
            project.Options.Profile.Debugger.OutputFile.Location = StepEnvironment.Local;

            result = await session.ExecuteAsync(new[] { 13u }, null);
            Assert.Equal("Local debugger output paths are not supported in this version of RAD Debugger.", result.Error.Value.Message);
        }
    }
}