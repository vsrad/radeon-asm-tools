using System;
using System.Collections.Generic;
using System.IO;
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
                new BuiltinActionFile { Type = StepEnvironment.Remote, CheckTimestamp = true, Path = "/home/parker/audio/master" },
                new BuiltinActionFile { Type = StepEnvironment.Remote, CheckTimestamp = false, Path = "/home/parker/audio/copy" },
                new BuiltinActionFile { Type = StepEnvironment.Local, CheckTimestamp = true, Path = ((CopyFileStep)steps[0]).LocalPath },
                new BuiltinActionFile { Type = StepEnvironment.Local, CheckTimestamp = false, Path = "non-existent-local-path" }
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
