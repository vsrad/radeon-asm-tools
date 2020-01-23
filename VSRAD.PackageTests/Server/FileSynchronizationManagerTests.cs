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

        private const DocumentSaveType _autosaveType = DocumentSaveType.SolutionDocuments;

        [Fact]
        public async Task SynchronizeProjectTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var sourceManager = new Mock<IProjectSourceManager>(MockBehavior.Strict);
            sourceManager.Setup((m) => m.SaveDocumentsAsync(_autosaveType)).Returns(Task.CompletedTask).Verifiable();
            var (project, syncer) = MakeProjectWithSyncer(channel.Object, sourceManager.Object, copySources: false);
            project.Setup((p) => p.SaveOptions()).Verifiable();

            await syncer.SynchronizeRemoteAsync();

            sourceManager.Verify(); // saves project documents
            project.Verify(); // saves project options
        }

        private static (Mock<IProject>, FileSynchronizationManager) MakeProjectWithSyncer(ICommunicationChannel channel, IProjectSourceManager sourceManager, bool copySources)
        {
            TestHelper.InitializePackageTaskFactory();
            var evaluator = new Mock<IMacroEvaluator>(MockBehavior.Strict);
            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectory));

            var project = new Mock<IProject>(MockBehavior.Strict);
            project.Setup((p) => p.RootPath).Returns(_projectRoot);
            project.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));

            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions(general: new GeneralProfileOptions(
                deployDirectory: _deployDirectory, copySources: copySources, autosaveSource: _autosaveType)));
            project.Setup((p) => p.Options).Returns(options);

            return (project, new FileSynchronizationManager(channel, project.Object, sourceManager));
        }
    }
}
