using Moq;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    public class ProgramTests
    {
        private static (Program, Mock<IEngineIntegration>, Mock<IEngineCallbacks>) InitProgram(string file)
        {
            var program = new Program(null);
            var integration = new Mock<IEngineIntegration>();
            var callbacks = new Mock<IEngineCallbacks>();
            integration.Setup((i) => i.GetActiveSourcePath()).Returns(file);
            program.AttachDebugger(integration.Object, callbacks.Object);
            return (program, integration, callbacks);
        }

        [Fact]
        public void TestBreakFrameWithoutBreakpoints()
        {
            var (program, _, _) = InitProgram("h.s");
            // When no breakpoints are set, the first break frame should be at the start of the file
            Helpers.VerifyBreakFrameLocation(program, "h.s", 0);
        }

        [Fact]
        public void TestStoppingAtBreakpoint()
        {
            var (program, integration, callbacks) = InitProgram("h.s");

            integration.Setup((i) => i.Execute(false)).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, null,
                new ExecutionCompletedEventArgs(new BreakTarget("h.s", new[] { 7u }, isStepping: false), true)));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.Execute(false), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Once);
            callbacks.Verify((c) => c.OnStepComplete(), Times.Never);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);
        }

        [Fact]
        public void TestStepping()
        {
            var (program, integration, callbacks) = InitProgram("h.s");

            integration.Setup((i) => i.Execute(false)).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, null,
                new ExecutionCompletedEventArgs(new BreakTarget("h.s", new[] { 7u }, isStepping: true), true)));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.Execute(false), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Never);
            callbacks.Verify((c) => c.OnStepComplete(), Times.Once);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);
        }
    }
}
