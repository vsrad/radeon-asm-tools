using System.IO.Pipes;
using System.Threading;
using Xunit;

namespace VSRAD.BuildTools
{
    public class IPCBridgeTests
    {
        [Fact]
        public void ConnectionTest()
        {
            new Thread(() =>
            {
                var server = new NamedPipeServerStream("vsrad-build-testpipe", PipeDirection.Out, 1);
                server.WaitForConnection();
                var message = new IPCBuildResult { ExitCode = 1, Stdout = "out", Stderr = "err" }.ToArray();
                server.Write(message, 0, message.Length);
                server.Close();
            }).Start();
            var bridge = new IPCBridge("vsrad-build-testpipe");
            var result = bridge.Build();
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("out", result.Stdout);
            Assert.Equal("err", result.Stderr);
        }
    }
}
