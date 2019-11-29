using Moq;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.PackageTests;
using Xunit;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools
{
    [Collection("Sequential")]
    public class BuildToolsServerTest
    {
        [Fact]
        public async Task SuccessfulBuildTestAsync()
        {
            TestHelper.InitializePackageTaskFactory();
            var project = TestHelper.MakeProjectWithProfile(new Dictionary<string, string>()
            {
                { RadMacros.BuildExecutable, "nemu" },
                { RadMacros.BuildArguments, "--sleep 10" },
                { RadMacros.BuildWorkingDirectory, "/old/home" }
            }, projectRoot: @"C:\Users\CFF\Repos\H");
            var projectSources = new[] { @"C:\Users\CFF\Repos\H\include\test.h", @"C:\Users\CFF\Repos\H\test.c" };
            var channel = new MockCommunicationChannel();
            var output = new Mock<IOutputWindowManager>();
            var deployManager = new Mock<IFileSynchronizationManager>();
            var errorProcessor = new Mock<IBuildErrorProcessor>(MockBehavior.Strict);
            output.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);
            errorProcessor
                .Setup((e) => e.ExtractMessagesAsync("stderr", It.IsAny<string>()))
                .Returns(Task.FromResult<IEnumerable<Message>>(Array.Empty<Message>()));

            var server = new BuildToolsServer(channel.Object, output.Object, errorProcessor.Object, deployManager.Object);
            server.SetProjectOnLoad(project); // starts the server

            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted
            {
                Status = ExecutionStatus.Completed,
                ExitCode = 0,
                Stdout = "day of flight",
                Stderr = "stderr"
            },
            (command) =>
            {
                Assert.Equal("nemu", command.Executable);
                Assert.Equal("--sleep 10", command.Arguments);
                Assert.Equal("/old/home", command.WorkingDirectory);
            });

            var message = await FetchResultOnClientAsync(server);
            deployManager.Verify((d) => d.SynchronizeRemoteAsync(), Times.Once);

            Assert.False(message.Skipped);
            Assert.Equal("", message.ServerError);
            Assert.Equal(0, message.ExitCode);
            Assert.Empty(message.ErrorMessages);
        }

        //[Fact]
        //public async Task PreprocessorTestAsync()
        //{
        //    TestHelper.InitializePackageTaskFactory();
        //    var project = TestHelper.MakeProjectWithProfile(new Dictionary<string, string>() {
        //        { RadMacros.BuildExecutable, "kuu" },
        //        { RadMacros.BuildWorkingDirectory, "/home/old" },
        //        { RadMacros.BuildPreprocessedSource, "preprocessed_source.build.tmp" }
        //    }, projectRoot: @"C:\Users\CFF\Preprocess");
        //    var channel = new MockCommunicationChannel();
        //    var output = new Mock<IOutputWindowManager>();

        //    var deployManager = new Mock<IFileSynchronizationManager>();
        //    output.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);

        //    var server = new BuildToolsServer(channel.Object, output.Object, sourceManager.Object, deployManager.Object);
        //    server.SetProjectOnLoad(project); // starts the server

        //    var timestamp = DateTime.Now;
        //    channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = timestamp }, (command) =>
        //    {
        //        Assert.Equal("/home/old", command.FilePath[0]);
        //        Assert.Equal("preprocessed_source.build.tmp", command.FilePath[1]);
        //    });
        //    channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
        //    channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched
        //    { Timestamp = timestamp.AddSeconds(1), Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("#define H") }, (_) => { });

        //    var message = await FetchResultOnClientAsync(server);
        //    Assert.Null(message.ServerError);
        //    Assert.Equal("#define H", message.PreprocessedSource);
        //}

        //[Fact]
        //public async Task PreprocessorErrorTestAsync()
        //{
        //    TestHelper.InitializePackageTaskFactory();
        //    var project = TestHelper.MakeProjectWithProfile(new Dictionary<string, string>() {
        //        { RadMacros.BuildExecutable, "kuu" },
        //        { RadMacros.BuildPreprocessedSource, "preprocessed_source.build.tmp" }
        //    }, projectRoot: @"C:\Users\CFF\Errors");
        //    var channel = new MockCommunicationChannel();
        //    var output = new Mock<IOutputWindowManager>();
        //    var sourceManager = new Mock<IProjectSourceManager>();
        //    var deployManager = new Mock<IFileSynchronizationManager>();
        //    output.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);

        //    var server = new BuildToolsServer(channel.Object, output.Object, sourceManager.Object, deployManager.Object);
        //    server.SetProjectOnLoad(project); // starts the server

        //    var timestamp = DateTime.Now;
        //    channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = timestamp }, (_) => { });
        //    channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
        //    channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = timestamp }, (_) => { });

        //    var message = await FetchResultOnClientAsync(server);
        //    Assert.Equal(BuildToolsServer.ErrorPreprocessorFileUnchanged, message.ServerError);

        //    channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.FileNotFound }, (_) => { });
        //    channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (_) => { });
        //    channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched { Status = FetchStatus.FileNotFound }, (_) => { });

        //    message = await FetchResultOnClientAsync(server);
        //    Assert.Equal(BuildToolsServer.ErrorPreprocessorFileNotCreated, message.ServerError);
        //}

        private static async Task<VSRAD.BuildTools.IPCBuildResult> FetchResultOnClientAsync(BuildToolsServer server)
        {
            VSRAD.BuildTools.IPCBuildResult message = null;
            var tcs = new TaskCompletionSource<bool>();

            new Thread(() =>
            {
                var client = new NamedPipeClientStream(server.PipeName);
                client.Connect();
                message = VSRAD.BuildTools.IPCBuildResult.Read(client);
                client.Close();
                tcs.SetResult(true);
            }).Start();

            await tcs.Task;
            return message;
        }
    }
}
