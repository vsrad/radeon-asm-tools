using Moq;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    public class ProgramTests
    {
        private static (Program, Mock<IEngineIntegration>, Mock<IEngineCallbacks>, Mock<IBreakpointManager>) InitProgram(string file)
        {
            var program = new Program(null);
            var integration = new Mock<IEngineIntegration>();
            var callbacks = new Mock<IEngineCallbacks>();
            var breakpointManager = new Mock<IBreakpointManager>();
            integration.Setup((i) => i.GetActiveProjectFile()).Returns(file);
            program.AttachDebugger(integration.Object, callbacks.Object, breakpointManager.Object);
            return (program, integration, callbacks, breakpointManager);
        }

        [Fact]
        public void TestBreakFrameWithoutBreakpoints()
        {
            var (program, _, _, _) = InitProgram("h.s");
            // When no breakpoints are set, the first break frame should be at the start of the file
            Helpers.VerifyBreakFrameLocation(program, "h.s", 0);
        }

        [Fact]
        public void TestSuccessfulExecutionWithBreakpoints()
        {
            var (program, integration, callbacks, breakpointManager) = InitProgram("h.s");
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("h.s", 0)).Returns(7);
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("h.s", 7)).Returns(13);
            integration.Setup((i) => i.ExecuteToLine(It.IsAny<uint>())).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, true));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(7), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Once);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(13), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(2));
            Helpers.VerifyBreakFrameLocation(program, "h.s", 13);
        }

        [Fact]
        public void TestRerunToLine()
        {
            var (program, integration, callbacks, breakpointManager) = InitProgram("h.s");
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("h.s", 0)).Returns(7);
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("hhhh.s", 0)).Returns(13);
            integration.Setup((i) => i.ExecuteToLine(It.IsAny<uint>())).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, true));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(7), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Once);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);

            integration.Setup((i) => i.GetActiveProjectFile()).Returns("hhhh.s");
            integration.Raise((i) => i.RerunRequested += null);
            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(7), Times.Exactly(2));
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(2));
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(13), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(3));
            Helpers.VerifyBreakFrameLocation(program, "hhhh.s", 13);
        }

        [Fact]
        public void TestExecutionFailed()
        {
            var (program, integration, callbacks, breakpointManager) = InitProgram("h.s");
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("h.s", 0)).Returns(7);
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("h.s", 7)).Returns(9);
            breakpointManager.Setup((b) => b.GetNextBreakpointLine("hhhh.s", 0)).Returns(13);

            // Execution to line 7 fails, the editor highlights line 7
            integration.Setup((i) => i.ExecuteToLine(It.IsAny<uint>())).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, false));
            program.ExecuteOnThread(null);

            integration.Verify((i) => i.ExecuteToLine(7), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Once);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);

            // The next run still executes to line 7 because of the previous error
            integration.Setup((i) => i.ExecuteToLine(It.IsAny<uint>())).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, true));
            program.ExecuteOnThread(null);

            integration.Verify((i) => i.ExecuteToLine(7), Times.Exactly(2));
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(2));
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);

            // Execution to line 7 succeeds, the next run moves to the next breakpoint (9),
            // and even though it fails, the editor hightlights line 9
            integration.Setup((i) => i.ExecuteToLine(It.IsAny<uint>())).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, false));
            program.ExecuteOnThread(null);

            integration.Verify((i) => i.ExecuteToLine(9), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(3));
            Helpers.VerifyBreakFrameLocation(program, "h.s", 9);

            // We switch to another file and the error state is magically reset (unlike rerun to line)
            integration.Setup((i) => i.GetActiveProjectFile()).Returns("hhhh.s");
            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(13), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(4));
            Helpers.VerifyBreakFrameLocation(program, "hhhh.s", 13);

            // RunToLine has higher priority than failed execution
            uint runToLine = 666;
            integration.Setup((i) => i.PopRunToLineIfSet("hhhh.s", out runToLine)).Returns(true);
            program.ExecuteOnThread(null);
            integration.Verify((i) => i.ExecuteToLine(666), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Exactly(5));
            Helpers.VerifyBreakFrameLocation(program, "hhhh.s", 666);
        }
    }
}
