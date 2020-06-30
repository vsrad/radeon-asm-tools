using Microsoft.Build.Framework;
using Moq;
using System;
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
            var pipeName = $"vsrad-{Guid.NewGuid()}";
            new Thread(() =>
            {
                var server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1);
                server.WaitForConnection();
                var message = new IPCBuildResult { ExitCode = 0 }.ToArray();
                server.Write(message, 0, message.Length);
                server.Close();
            }).Start();

            var engine = new Mock<IBuildEngine>();
            var task = new RemoteBuildTask
            {
                PipeName = pipeName,
                BuildEngine = engine.Object
            };
            Assert.True(task.Execute());

            engine.Verify((e) => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()), Times.Never);
        }

        [Fact]
        public void ServerErrorTest()
        {
            var pipeName = $"vsrad-{Guid.NewGuid()}";
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
                PipeName = pipeName,
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
                PipeName = $"vsrad-{Guid.NewGuid()}",
                BuildEngine = engine.Object
            };
            Assert.False(task.Execute());

            engine.Verify((e) => e.LogErrorEvent(It.Is<BuildErrorEventArgs>(
                (a) => a.Message == RemoteBuildTask.TimeoutError)), Times.Once);
        }
    }
}
