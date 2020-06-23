using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionRunnerTests
    {
        [Fact]
        public async Task CopyFileStepErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, RemotePath = "/home/mizu/machete/key3_49", LocalPath = "" },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };
            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound } }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound });
            var runner = new ActionRunner(channel.Object);
            var result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not found on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) } }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) });
            result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not changed on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task ExecuteStepErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, RemotePath = "/home/parker/audio/unchecked", LocalPath = "" }, // should not be run
            };
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stdout = "", Stderr = "" });
            var runner = new ActionRunner(channel.Object);
            var result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process could not be started on the remote machine. Make sure the path to the executable is specified correctly.", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (could not launch)\r\n", result.StepResults[0].Log);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut, Stdout = "...\n", Stderr = "Could not prepare master DVD, deadline exceeded.\n\n" });
            result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Execution timeout is exceeded. dvd-prepare process on the remote machine is terminated.", result.StepResults[0].Warning);
            Assert.Equal("Captured stdout (timed out):\r\n...\r\nCaptured stderr (timed out):\r\nCould not prepare master DVD, deadline exceeded.\r\n", result.StepResults[0].Log);

            /* Non-zero exit code results in a successful run with a warning */
            steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" } };
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 1, Stdout = "", Stderr = "Looks like you fell asleep ¯\\_(ツ)_/¯\n\n" });
            result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process exited with a non-zero code (1). Check your application or debug script output in Output -> RAD Debug.", result.StepResults[0].Warning);
            Assert.Equal("Captured stderr (exit code 1):\r\nLooks like you fell asleep ¯\\_(ツ)_/¯\r\n", result.StepResults[0].Log);
        }

        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, RemotePath = "/home/mizu/machete/tweened.tvpp", LocalPath = Path.GetTempFileName() }
            };
            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) } }); // init timestamp fetch
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "", Stderr = "" });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = Encoding.UTF8.GetBytes("file-contents") });
            var runner = new ActionRunner(channel.Object);
            var result = await runner.RunAsync(steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);
            Assert.True(result.StepResults[1].Successful);
            Assert.Equal("", result.StepResults[1].Warning);
            Assert.Equal("", result.StepResults[1].Log);
            Assert.Equal("file-contents", File.ReadAllText(((CopyFileStep)steps[1]).LocalPath));
            File.Delete(((CopyFileStep)steps[1]).LocalPath);
        }

        [Fact]
        public async Task VerifiesTimestampsTestAsync()
        {
            var channel = new MockCommunicationChannel();

            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, RemotePath = "/home/parker/audio/checked", LocalPath = Path.GetTempFileName() },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, RemotePath = "/home/parker/audio/unchecked", LocalPath = Path.GetTempFileName() },
            };
            var auxFiles = new List<BuiltinActionFile>
            {
                new BuiltinActionFile { Location = StepEnvironment.Remote, CheckTimestamp = true, Path = "/home/parker/audio/master" },
                new BuiltinActionFile { Location = StepEnvironment.Remote, CheckTimestamp = false, Path = "/home/parker/audio/copy" },
                new BuiltinActionFile { Location = StepEnvironment.Local, CheckTimestamp = true, Path = ((CopyFileStep)steps[0]).LocalPath },
                new BuiltinActionFile { Location = StepEnvironment.Local, CheckTimestamp = false, Path = "non-existent-local-path" }
            };
            channel.ThenRespond(new IResponse[]
            {
                new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(100) },
                new MetadataFetched { Status = FetchStatus.FileNotFound }
            }, (commands) =>
            {
                Assert.Equal(2, commands.Count);
                Assert.Equal(new[] { "/home/parker/audio/checked" }, ((FetchMetadata)commands[0]).FilePath);
                Assert.Equal(new[] { "/home/parker/audio/master" }, ((FetchMetadata)commands[1]).FilePath);
            });
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(
                new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepChecked") },
                (command) => Assert.Equal(new[] { "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(
                new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepUnchecked") },
                (command) => Assert.Equal(new[] { "/home/parker/audio/unchecked" }, command.FilePath));
            var runner = new ActionRunner(channel.Object);
            await runner.RunAsync(steps, auxFiles);
            Assert.True(channel.AllInteractionsHandled);

            Assert.Equal(DateTime.FromFileTime(100), runner.GetInitialFileTimestamp("/home/parker/audio/checked"));
            Assert.Equal(default, runner.GetInitialFileTimestamp("/home/parker/audio/master"));
            Assert.Equal(File.GetCreationTime(((CopyFileStep)steps[0]).LocalPath), runner.GetInitialFileTimestamp(((CopyFileStep)steps[0]).LocalPath));

            Assert.Equal("TestCopyStepChecked", File.ReadAllText(((CopyFileStep)steps[0]).LocalPath));
            File.Delete(((CopyFileStep)steps[0]).LocalPath);
            Assert.Equal("TestCopyStepUnchecked", File.ReadAllText(((CopyFileStep)steps[1]).LocalPath));
            File.Delete(((CopyFileStep)steps[1]).LocalPath);
        }
    }
}
