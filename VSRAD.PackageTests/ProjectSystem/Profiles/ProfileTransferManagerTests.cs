using Moq;
using System.Collections.Generic;
using System.IO;
using VSRAD.Package.Options;
using Xunit;
using static VSRAD.Package.Options.ProjectOptions;

namespace VSRAD.Package.ProjectSystem.Profiles.Tests
{
    public class ProfileTransferManagerTests
    {
        public object Dictionaryoptions { get; private set; }

        private ProjectOptions CreateTestOptions()
        {
            var options = new ProjectOptions();
            options.AddProfile("haruko", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "vespa")));
            options.AddProfile("mamimi", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "bridge")));
            return options;
        }

        [Fact]
        public void TransferTest()
        {
            var options = CreateTestOptions();
            var nameResolver = new Mock<ResolveImportNameConflict>(MockBehavior.Strict);
            nameResolver.Setup((n) => n("haruko")).Returns("haruhara").Verifiable();

            var manager = new ProfileTransferManager(options, nameResolver.Object);

            var tmpFile = Path.GetTempFileName();
            manager.Export(tmpFile);
            options.RemoveProfile("mamimi");
            manager.Import(tmpFile);
            File.Delete(tmpFile);

            options.UpdateProfiles(new Dictionary<string, ProfileOptions>
                { { "haruko", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "space")) } }, nameResolver.Object);

            nameResolver.Verify();
            Assert.Equal(3, options.Profiles.Count);
            Assert.Equal("bridge", options.Profiles["mamimi"].General.RemoteMachine);
            Assert.Equal("space", options.Profiles["haruko"].General.RemoteMachine);
            Assert.Equal("vespa", options.Profiles["haruhara"].General.RemoteMachine);
        }

        [Fact]
        public void ImportSkipConflictingNameTest()
        {
            var options = CreateTestOptions();
            var nameResolver = new Mock<ResolveImportNameConflict>(MockBehavior.Strict);
            nameResolver.Setup((n) => n("haruko")).Returns((string)null).Verifiable();

            var manager = new ProfileTransferManager(options, nameResolver.Object);

            var tmpFile = Path.GetTempFileName();
            manager.Export(tmpFile);
            options.RemoveProfile("mamimi");
            manager.Import(tmpFile);
            File.Delete(tmpFile);

            options.UpdateProfiles(new Dictionary<string, ProfileOptions>
                { { "haruko", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "space")) } }, nameResolver.Object);

            nameResolver.Verify();
            Assert.Equal(2, options.Profiles.Count);
            Assert.Equal("bridge", options.Profiles["mamimi"].General.RemoteMachine);
            Assert.Equal("space", options.Profiles["haruko"].General.RemoteMachine);
        }
    }
}