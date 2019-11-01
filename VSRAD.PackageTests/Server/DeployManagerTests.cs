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
        public async Task FirstDeployIsFullProjectAsync()
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
            options.AddProfile("Default", new ProfileOptions());
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\head.txt");
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\tea.txt");

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            packer
                .Setup((p) => p.PackDirectory(It.IsAny<string>()))
                .Callback((string projectDirectory) =>
                {
                    Assert.Equal(projectDirectory, _projectRoot);
                });

            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackDirectory(_projectRoot), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RedeployPackageIfChangedRemoteDirectoryAsync()
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
            options.AddProfile("Default", new ProfileOptions());
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\head.txt");
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\tea.txt");

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            packer
                .Setup((p) => p.PackDirectory(It.IsAny<string>()))
                .Callback((string projectDirectory) =>
                {
                    Assert.Equal(projectDirectory, _projectRoot);
                });

            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackDirectory(_projectRoot), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();


            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectorySecond));
            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();

            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackDirectory(_projectRoot), Times.Exactly(2));
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task DeployOnlyChangedSourcesTestAsync()
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
            options.AddProfile("Default", new ProfileOptions());
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\head.txt");
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\tea.txt");

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackDirectory(_projectRoot), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();

            packer.Reset();
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\ribbon.txt");
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\bow.txt");
            events.Raise((e) => e.SourceFileChanged += null, _projectRoot + @"\ribbon.txt");
            var changed = new HashSet<string>() { _projectRoot + @"\ribbon.txt", _projectRoot + @"\bow.txt" };

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            packer
                .Setup((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()))
                .Callback((IEnumerable<DeployItem> files) =>
                {
                    Assert.True(changed.Count == files.Count());
                    foreach (var file in files)
                        Assert.Contains(file.ActualPath, changed);
                });
            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();
        }

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
        public async Task DeployAdditionalFilesAsync()
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
            
            var additionaFilePathFirst = Path.GetTempFileName();
            var additionaFilePathSecond = Path.GetTempFileName();

            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions(general: new GeneralProfileOptions(additionalSources: $"{additionaFilePathFirst};{additionaFilePathSecond}")));
            project.Setup((p) => p.Options).Returns(options);

            var deployManager = new FileSynchronizationManager(channel.Object, packer.Object, events.Object, project.Object, projectSourceManager.Object);

            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            //packer
            //    .Setup((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()))
            //    .Callback((IEnumerable<DeployItem> files) =>
            //    {
            //        Assert.True(files.Count() == 2);
            //    });

            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackDirectory(_projectRoot), Times.Once);
            packer.Verify((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();

            packer.Reset();
            File.SetLastWriteTimeUtc(additionaFilePathFirst, System.DateTime.UtcNow);
            channel.ThenExpect<DebugServer.IPC.Commands.Deploy>();
            packer
                .Setup((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()))
                .Callback((IEnumerable<DeployItem> files) =>
                {
                    Assert.True(files.Count() == 1);
                    Assert.Equal(files.First().ActualPath, additionaFilePathFirst);
                });

            await deployManager.SynchronizeRemoteAsync();
            packer.Verify((p) => p.PackItems(It.IsAny<IEnumerable<DeployItem>>()), Times.Once);
            packer.VerifyNoOtherCalls();
            await deployManager.SynchronizeRemoteAsync();
            packer.VerifyNoOtherCalls();

            File.Delete(additionaFilePathFirst);
            File.Delete(additionaFilePathSecond);
        }
    }
}
