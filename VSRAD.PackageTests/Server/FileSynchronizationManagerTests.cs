using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class FileSynchronizationManagerTests
    {
        private const string _projectRoot = @"C:\Users\Mami\repos\Teapot";
        private const string _deployDirectory = "/home/kyubey/projects";
        private const string _deployDirectorySecond = @"C:\Users\Mami\repos\TeapotSecond";

        [Fact]
        public async Task DeployOnlyIfCopyFilesEnabledAsync()
        {
            TestHelper.InitializePackageTaskFactory();
            var channel = new MockCommunicationChannel();
            var events = new Mock<IProjectEvents>();
            var packer = new Mock<IDeployFilePacker>();
            var project = new Mock<IProject>(MockBehavior.Strict);
            project.Setup((p) => p.RootPath).Returns(_projectRoot);
            var projectSourceManager = new Mock<IProjectSourceManager>();
            var evaluator = new Mock<IMacroEvaluator>(MockBehavior.Strict);
            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectory));
            project.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));
            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "vespa", deployDirectory: "C:\\Hello\\World", copySources: false)));
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SaveUserJsonOnRunAsync()
        {
            int saveOptionsCount = 0;
            TestHelper.InitializePackageTaskFactory();
            var channel = new MockCommunicationChannel();
            var events = new Mock<IProjectEvents>();
            var packer = new Mock<IDeployFilePacker>();
            var project = new Mock<IProject>(MockBehavior.Strict);
            project.Setup((p) => p.RootPath).Returns(_projectRoot);
            project.Setup((p) => p.SaveOptions()).Callback(() => saveOptionsCount++);
            var projectSourceManager = new Mock<IProjectSourceManager>();
            var evaluator = new Mock<IMacroEvaluator>(MockBehavior.Strict);
            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectory));
            project.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));
            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions());
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            await deployManager.SynchronizeRemoteAsync();
            Assert.Equal(1, saveOptionsCount);

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            await deployManager.SynchronizeRemoteAsync();
            Assert.Equal(2, saveOptionsCount);
        }
    }
}
