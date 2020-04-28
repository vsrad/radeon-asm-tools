﻿using Microsoft.VisualStudio.ProjectSystem;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
                .Setup((m) => m.SaveDocumentsAsync(DocumentSaveType.OpenDocuments))
                .Returns(Task.CompletedTask).Verifiable();
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                copySources: false), channel.Object, fileProvider: null, sourceManager.Object);
            project.Setup((p) => p.SaveOptions()).Verifiable();

            await syncer.SynchronizeRemoteAsync();

            sourceManager.Verify(); // saves project documents
            project.Verify(); // saves project options
        }

        [Fact]
        public async Task DeployProjectFilesTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var fileProvider = new Mock<IProjectItemProvider>(MockBehavior.Strict);
            fileProvider.Setup((p) => p.GetItemsAsync()).ReturnsAsync(new List<IProjectItem>()
            {
                MakeProjectItem("source.txt"),
                MakeProjectItem("Include/include.txt")
            });
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                deployDirectory: _deployDirectory, copySources: true), channel.Object, fileProvider.Object);
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

            // does not redeploy when nothing is changed 
            archive = null;
            channel.ThenExpect<Deploy>((deploy) => archive = deploy.Data);
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
            var fileProvider = new Mock<IProjectItemProvider>(MockBehavior.Strict);
            fileProvider.Setup((p) => p.GetItemsAsync()).ReturnsAsync(new List<IProjectItem>()
            {
                MakeProjectItem("source.txt"),
                MakeProjectItem("Include/include.txt")
            });
            var (project, syncer) = MakeProjectWithSyncer(new GeneralProfileOptions(
                deployDirectory: _deployDirectory, copySources: true,
                additionalSources: $@"{_fixturesDir}\AdditionalSources;{_fixturesDir}\separate.txt"), channel.Object, fileProvider.Object);
            project.Setup((p) => p.SaveOptions());

            byte[] archive = null;
            channel.ThenExpect<Deploy>((deploy) => archive = deploy.Data);

            await syncer.SynchronizeRemoteAsync();

            Assert.NotNull(archive);
            var deployedItems = ReadZipItems(archive);
            var expectedItems = new HashSet<string> { "source.txt", "Include/include.txt", "separate.txt", "notice.txt", "Nested/message.txt" };
            Assert.Equal(expectedItems, deployedItems);

            // does not redeploy when nothing is changed
            archive = null;
            channel.ThenExpect<Deploy>((deploy) => archive = deploy.Data);
            await syncer.SynchronizeRemoteAsync();
            Assert.Null(archive);

            // profile changed
            channel.RaiseConnectionStateChanged();

            await syncer.SynchronizeRemoteAsync();
            Assert.NotNull(archive);
            deployedItems = ReadZipItems(archive);
            Assert.Equal(expectedItems, deployedItems);
        }

        private static (Mock<IProject>, FileSynchronizationManager) MakeProjectWithSyncer(GeneralProfileOptions generalOptions, ICommunicationChannel channel, IProjectItemProvider fileProvider, IProjectSourceManager sourceManager = null)
        {
            TestHelper.InitializePackageTaskFactory();
            var evaluator = new Mock<IMacroEvaluator>(MockBehavior.Strict);
            evaluator.Setup((e) => e.GetMacroValueAsync(RadMacros.DeployDirectory)).Returns(Task.FromResult(_deployDirectory));

            var project = new Mock<IProject>(MockBehavior.Strict);
            project.Setup((p) => p.RootPath).Returns(_projectRoot);
            project.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint[]>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));

            var options = new ProjectOptions();
            options.AddProfile("Default", new ProfileOptions(general: generalOptions));
            project.Setup((p) => p.Options).Returns(options);

            sourceManager = sourceManager ?? new Mock<IProjectSourceManager>().Object;
            var syncer = new FileSynchronizationManager(channel, project.Object, sourceManager, null)
            {
                _projectItemProvider = fileProvider
            };
            return (project, syncer);
        }

        private static IProjectItem MakeProjectItem(string relativePath, string fullPath = null, string link = "")
        {
            var item = new Mock<IProjectItem>(MockBehavior.Strict);
            item.Setup((i) => i.EvaluatedIncludeAsFullPath).Returns(fullPath ?? _projectRoot + "/" + relativePath);
            item.Setup((i) => i.EvaluatedIncludeAsRelativePath).Returns(relativePath);
            item.Setup((i) => i.Metadata.GetEvaluatedPropertyValueAsync("Link")).ReturnsAsync(link);
            return item.Object;
        }

        private static HashSet<string> ReadZipItems(byte[] zipBytes)
        {
            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream))
                return archive.Entries.Select(entry => entry.FullName).ToHashSet();
        }
    }
}
