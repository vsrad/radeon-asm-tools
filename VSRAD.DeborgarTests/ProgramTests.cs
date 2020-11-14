using Moq;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    public class ProgramTests
    {
        private static (Program, Mock<IEngineIntegration>, Mock<IEngineCallbacks>) InitProgram()
        {
            var program = new Program(null);
            var integration = new Mock<IEngineIntegration>();
            var callbacks = new Mock<IEngineCallbacks>();
            program.AttachDebugger(integration.Object, callbacks.Object);
            return (program, integration, callbacks);
        }

        [Fact]
        public void TestStoppingAtBreakpoint()
        {
            var (program, integration, callbacks) = InitProgram();

            integration.Setup((i) => i.Execute(false)).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, null,
                new ExecutionCompletedEventArgs("h.s", new[] { 7u }, isStepping: false)));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.Execute(false), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Once);
            callbacks.Verify((c) => c.OnStepComplete(), Times.Never);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);
        }

        [Fact]
        public void TestStepping()
        {
            var (program, integration, callbacks) = InitProgram();

            integration.Setup((i) => i.Execute(false)).Callback(() =>
                integration.Raise((i) => i.ExecutionCompleted += null, null,
                new ExecutionCompletedEventArgs("h.s", new[] { 7u }, isStepping: true)));

            program.ExecuteOnThread(null);
            integration.Verify((i) => i.Execute(false), Times.Once);
            callbacks.Verify((c) => c.OnBreakComplete(), Times.Never);
            callbacks.Verify((c) => c.OnStepComplete(), Times.Once);
            Helpers.VerifyBreakFrameLocation(program, "h.s", 7);
        }
    }
}
