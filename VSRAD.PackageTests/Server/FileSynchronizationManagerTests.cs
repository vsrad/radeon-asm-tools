using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class FileSynchronizationManagerTests
    {
        private static readonly string _fixturesDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + @"\Server\Fixtures";
        private static readonly string _projectRoot = _fixturesDir + @"\Project";

        private const string _deployDirectory = "/home/kyubey/projects";

        [Fact]
        public async Task SynchronizeProjectTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var sourceManager = new Mock<IProjectSourceManager>(MockBehavior.Strict);
            sourceManager
                .Setup((m) => m.SaveDocumentsAsync(DocumentSaveType.SolutionDocuments))
                .Returns(Task.CompletedTask).Verifiable();
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                copySources: false, autosaveSource: DocumentSaveType.SolutionDocuments),
                channel.Object, sourceManager.Object);
            project.Setup((p) => p.SaveOptions()).Verifiable();

            await syncer.SynchronizeRemoteAsync();

            sourceManager.Verify(); // saves project documents
            project.Verify(); // saves project options
        }

        [Fact]
        public async Task DeployProjectFilesTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                deployDirectory: _deployDirectory, copySources: true), channel.Object);
            project.Setup((p) => p.SaveOptions());

            byte[] archive = null;
            channel.ThenExpect<Deploy>((deploy) =>
            {
                Assert.Equal(_deployDirectory, deploy.Destination);
                archive = deploy.Data;
            });

            await syncer.SynchronizeRemoteAsync();

            Assert.NotNull(archive);
            var deployedItems = ReadZipItems(archive);
            Assert.Equal(new HashSet<string> { "source.txt", "Include/include.txt" }, deployedItems);

            archive = null;
            channel.ThenExpect<Deploy>((deploy) => archive = deploy.Data);

            // does not redeploy when nothing is changed
            await syncer.SynchronizeRemoteAsync();
            Assert.Null(archive);

            File.SetLastWriteTime($@"{_projectRoot}\source.txt", DateTime.Now);

            await syncer.SynchronizeRemoteAsync();
            Assert.NotNull(archive);
            deployedItems = ReadZipItems(archive);
            Assert.Equal(new HashSet<string> { "source.txt" }, deployedItems);
        }

        [Fact]
        public async Task DeployFilesWithAdditionalSourcesTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                deployDirectory: _deployDirectory, copySources: true,
                additionalSources: $@"{_fixturesDir}\AdditionalSources;{_fixturesDir}\separate.txt"), channel.Object);
            project.Setup((p) => p.SaveOptions());

            byte[] archive = null;
            channel.ThenExpect<Deploy>((deploy) => archive = deploy.Data);

            await syncer.SynchronizeRemoteAsync();

            Assert.NotNull(archive);
            var deployedItems = ReadZipItems(archive);
            var expectedItems = new HashSet<string> { "source.txt", "Include/include.txt", "separate.txt", "notice.txt", "Nested/message.txt" };
            Assert.Equal(expectedItems, deployedItems);
        }

        private static (Mock<IProject>, FileSynchronizationManager) MakeProjectWithSyncer(GeneralProfileOptions generalOptions, ICommunicationChannel channel, IProjectSourceManager sourceManager = null)
        {
            TestHelper.InitializePackageTaskFactory();
            var evaluator = new Mock<IMacroEvaluator>(MockBehavior.Strict);
            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectory));

            var project = new Mock<IProject>(MockBehavior.Strict);
            project.Setup((p) => p.RootPath).Returns(_projectRoot);
            project.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));

            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions(general: generalOptions));
            project.Setup((p) => p.Options).Returns(options);

            return (project, new FileSynchronizationManager(channel, project.Object, sourceManager ?? new Mock<IProjectSourceManager>().Object));
        }

        private static HashSet<string> ReadZipItems(byte[] archive)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(tempFile, archive);
            try
            {
                using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Read))
                    return zip.Entries.Select(entry => entry.FullName).ToHashSet();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
