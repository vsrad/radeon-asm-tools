using Microsoft.Build.Framework;
using Moq;
using VSRAD.BuildTools;
using Xunit;

namespace VSRAD.BuildToolsTests
{
    public class RemoteBuildTaskTests
    {
        [Fact]
        public void TimeoutTest()
        {
            var engine = new Mock<IBuildEngine>();
            var task = new RemoteBuildTask
            {
                ProjectDir = "/home/iwlain",
                BuildEngine = engine.Object
            };
            task.Execute();

            engine.Verify((e) => e.LogErrorEvent(It.Is<BuildErrorEventArgs>((a) => a.Message == RemoteBuildTask.TimeoutError)), Times.Once);
        }
    }
}
