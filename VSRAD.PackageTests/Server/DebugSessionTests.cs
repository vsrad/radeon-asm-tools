﻿using Moq;
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
            Assert.Null(breakState.DispatchParameters);
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
            Assert.False(channel.AllInteractionsHandled);
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

        [Fact]
        public async Task SuccessfulRunWithStatusFileTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile(remoteWorkDir: "/remote/workdir").Object;
            project.Options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            project.Options.Profile.Debugger.Steps.Add(new ExecuteStep { Executable = "va11" });
            project.Options.Profile.Debugger.OutputFile.Path = "/glitch/city";
            project.Options.Profile.Debugger.StatusFile.Path = "/glitch/city/status";

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound }, new MetadataFetched { Status = FetchStatus.FileNotFound } }); // init timestamp fetch
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new IResponse[]
            {
                // status
                new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes(@"
grid size (8192, 0, 0)
group size (512, 0, 0)
wave size 32
comment 115200"), Timestamp = DateTime.Now },
                new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now } // output
            });

            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, null);
            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "watch" }.ToList()));
            Assert.True(channel.AllInteractionsHandled);
            Assert.Null(result.Error);
            Assert.NotNull(result.BreakState.DispatchParameters);
            Assert.Equal<uint>(8192 / 512, result.BreakState.DispatchParameters.DimX);
            Assert.Equal<uint>(512, result.BreakState.DispatchParameters.GroupSize);
            Assert.Equal<uint>(32, result.BreakState.DispatchParameters.WaveSize);
            Assert.False(result.BreakState.DispatchParameters.NDRange3D);
            Assert.Equal("115200", result.BreakState.DispatchParameters.StatusString);
        }

        [Fact]
        public async Task EmptyStatusFileTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var project = TestHelper.MakeProjectWithProfile(remoteWorkDir: "/remote/workdir").Object;
            project.Options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            project.Options.Profile.Debugger.Steps.Add(new ExecuteStep { Executable = "va11" });
            project.Options.Profile.Debugger.OutputFile.Path = "/glitch/city";
            project.Options.Profile.Debugger.StatusFile.Path = "/glitch/city/status";

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound }, new MetadataFetched { Status = FetchStatus.FileNotFound } }); // init timestamp fetch
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new IResponse[]
            {
                new ResultRangeFetched { Status = FetchStatus.Successful, Data = Array.Empty<byte>(), Timestamp = DateTime.Now }, // status
                new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now } // output
            });

            var session = new DebugSession(project, channel.Object, new Mock<IFileSynchronizationManager>().Object, null);
            var result = await session.ExecuteAsync(new[] { 13u }, new ReadOnlyCollection<string>(new[] { "watch" }.ToList()));
            Assert.True(channel.AllInteractionsHandled);
            Assert.Null(result.BreakState);
            Assert.StartsWith("Could not read dispatch parameters from the status file", result.Error.Value.Message);
        }
    }
}