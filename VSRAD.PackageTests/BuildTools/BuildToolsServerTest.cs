using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
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
            var channel = new MockCommunicationChannel();
            var deployManager = new Mock<IFileSynchronizationManager>();
            var errorProcessor = new Mock<IBuildErrorProcessor>(MockBehavior.Strict);
            errorProcessor
                .Setup((e) => e.ExtractMessagesAsync("stderr", It.IsAny<string>()))
                .Returns(Task.FromResult<IEnumerable<Message>>(Array.Empty<Message>()));
            var server = StartBuildServer(project, channel.Object, deployManager.Object, errorProcessor.Object);

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

        [Fact]
        public async Task PreprocessorTestAsync()
        {
            var preprocessorLocalFile = Path.GetTempFileName();

            TestHelper.InitializePackageTaskFactory();
            var project = TestHelper.MakeProjectWithProfile(
                new Dictionary<string, string>() {
                    { RadMacros.PreprocessorExecutable, "kuu" },
                    { RadMacros.PreprocessorArguments, "--away" },
                    { RadMacros.PreprocessorWorkingDirectory, "/home/old" },
                    { RadMacros.PreprocessorOutputPath, "preprocessed_source.build.tmp" },
                    { RadMacros.PreprocessorLocalPath, preprocessorLocalFile }
                },
                projectRoot: @"C:\Users\CFF\Preprocess",
                profile: new Options.ProfileOptions(build: new Options.BuildProfileOptions(runPreprocessor: true)));
            var channel = new MockCommunicationChannel();
            var server = StartBuildServer(project, channel.Object);

            var timestamp = DateTime.Now;
            channel.ThenRespond<FetchMetadata, MetadataFetched>(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = timestamp }, (command) =>
            {
                Assert.Equal("/home/old", command.FilePath[0]);
                Assert.Equal("preprocessed_source.build.tmp", command.FilePath[1]);
            });
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted
            { Status = ExecutionStatus.Completed, ExitCode = 0 },
            (command) =>
            {
                Assert.Equal("kuu", command.Executable);
                Assert.Equal("--away", command.Arguments);
                Assert.Equal("/home/old", command.WorkingDirectory);
            });
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(new ResultRangeFetched
            { Timestamp = timestamp.AddSeconds(1), Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("#define H") }, (_) => { });

            var message = await FetchResultOnClientAsync(server);

            Assert.False(message.Skipped);
            Assert.Equal("", message.ServerError);
            Assert.Equal(0, message.ExitCode);
            Assert.Empty(message.ErrorMessages);

            Assert.Equal("#define H", File.ReadAllText(preprocessorLocalFile));
            File.Delete(preprocessorLocalFile);
        }

        private static BuildToolsServer StartBuildServer(IProject project, ICommunicationChannel channel, IFileSynchronizationManager deployManager = null, IBuildErrorProcessor errorProcessor = null)
        {
            deployManager = deployManager ?? new Mock<IFileSynchronizationManager>().Object;
            errorProcessor = errorProcessor ?? new Mock<IBuildErrorProcessor>().Object;

            var output = new Mock<IOutputWindowManager>();
            output.Setup((w) => w.GetExecutionResultPane()).Returns(new Mock<IOutputWindowWriter>().Object);

            var server = new BuildToolsServer(channel, output.Object, errorProcessor, deployManager);
            server.SetProjectOnLoad(project); // starts the server
            return server;
        }

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
