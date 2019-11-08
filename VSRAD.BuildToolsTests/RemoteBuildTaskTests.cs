using Microsoft.Build.Framework;
using Moq;
using System.IO.Pipes;
using System.Threading;
using VSRAD.BuildTools;
using Xunit;

namespace VSRAD.BuildToolsTests
{
    public class RemoteBuildTaskTests
    {
        [Fact]
        public void SuccessfulBuildTest()
        {
            var pipeName = IPCBuildResult.GetIPCPipeName(@"C:\One\One");
            new Thread(() =>
            {
                var server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1);
                server.WaitForConnection();
                var message = new IPCBuildResult { ExitCode = 0, Stdout = "out", Stderr = "err" }.ToArray();
                server.Write(message, 0, message.Length);
                server.Close();
            }).Start();

            var engine = new Mock<IBuildEngine>();
            var task = new RemoteBuildTask
            {
                ProjectDir = @"C:\One\One",
                BuildEngine = engine.Object
            };
            Assert.True(task.Execute());

            engine.Verify((e) => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()), Times.Never);
        }

        [Fact]
        public void ServerErrorTest()
        {
            var pipeName = IPCBuildResult.GetIPCPipeName(@"C:\One\One");
            new Thread(() =>
            {
                var server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1);
                server.WaitForConnection();
                var message = new IPCBuildResult { ServerError = "Return to your seat" }.ToArray();
                server.Write(message, 0, message.Length);
                server.Close();
            }).Start();

            var engine = new Mock<IBuildEngine>();
            var task = new RemoteBuildTask
            {
                ProjectDir = @"C:\One\One",
                BuildEngine = engine.Object
            };
            Assert.False(task.Execute());

            engine.Verify((e) => e.LogErrorEvent(It.Is<BuildErrorEventArgs>(
                (a) => a.Message == RemoteBuildTask.ServerErrorPrefix + "Return to your seat")), Times.Once);
        }

        [Fact]
        public void TimeoutTest()
        {
            var engine = new Mock<IBuildEngine>();
            var task = new RemoteBuildTask
            {
                ProjectDir = @"C:\Users\Tulip\Source\Repos\GoodGuysPoppinBadGuys",
                BuildEngine = engine.Object
            };
            Assert.False(task.Execute());

            engine.Verify((e) => e.LogErrorEvent(It.Is<BuildErrorEventArgs>(
                (a) => a.Message == RemoteBuildTask.TimeoutError)), Times.Once);
        }
    }
}
