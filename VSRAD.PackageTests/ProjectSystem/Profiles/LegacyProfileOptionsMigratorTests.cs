using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Profiles;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class LegacyProfileOptionsMigratorTests
    {
        private static readonly string _fixturesDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + @"\ProjectSystem\Profiles\Fixtures";
        private static readonly string _legacyJson = File.ReadAllText(Path.Combine(_fixturesDir, "DebuggerProfileOptions.conf.json"));

        [Fact]
        public void ConvertsDebugProfileOptionsToActionTest()
        {
            var conf = JObject.Parse(_legacyJson);
            LegacyProfileOptionsMigrator.ConvertOldOptionsIfPresent(conf);
            var converted = conf.ToObject<ProjectOptions>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });

            var convertedAction = converted.Profiles["Default"].Actions[1];
            Assert.Equal(2, convertedAction.Steps.Count);
            var step1 = (ExecuteStep)convertedAction.Steps[0];
            Assert.Equal(StepEnvironment.Remote, step1.Environment);
            Assert.Equal("debug", step1.Executable);
            var step2 = (ReadDebugDataStep)convertedAction.Steps[1];
            Assert.False(step2.BinaryOutput);
            Assert.Equal(1, step2.OutputOffset);
            Assert.Equal(StepEnvironment.Remote, step2.OutputFile.Location);
            Assert.Equal("output-file", step2.OutputFile.Path);
            Assert.False(step2.OutputFile.CheckTimestamp);
            Assert.Equal(StepEnvironment.Remote, step2.WatchesFile.Location);
            Assert.Equal("watches-file", step2.WatchesFile.Path);
            Assert.False(step2.WatchesFile.CheckTimestamp);
            Assert.Equal(StepEnvironment.Local, step2.DispatchParamsFile.Location);
            Assert.Equal("status-file", step2.DispatchParamsFile.Path);
            Assert.True(step2.DispatchParamsFile.CheckTimestamp);

            Assert.Equal("Debug (Old)", converted.Profiles["Default"].MenuCommands.DebugAction);
        }

        [Fact]
        public void DoesNotReplaceExistingDebugActionTest()
        {
            var conf = JObject.Parse(_legacyJson);
            LegacyProfileOptionsMigrator.ConvertOldOptionsIfPresent(conf);
            var converted = conf.ToObject<ProjectOptions>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });

            Assert.Equal(2, converted.Profiles["Default"].Actions.Count);
            var unrelatedAction = converted.Profiles["Default"].Actions[0];
            Assert.Equal("Debug", unrelatedAction.Name);
            Assert.Single(unrelatedAction.Steps);
            Assert.Equal("Unrelated Action That Should Not Be Replaced", ((OpenInEditorStep)unrelatedAction.Steps[0]).Path);
            Assert.Equal("Debug (Old)", converted.Profiles["Default"].Actions[1].Name);

            Assert.Single(converted.Profiles["Second Profile"].Actions);
            Assert.Equal("Debug", converted.Profiles["Second Profile"].Actions[0].Name);
        }
    }
}
