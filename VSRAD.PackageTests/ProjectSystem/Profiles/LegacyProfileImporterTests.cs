using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Profiles;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class LegacyProfileImporterTests
    {
        private static readonly string _fixturesDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + @"\ProjectSystem\Profiles\Fixtures";
        private static readonly string _legacyJson = File.ReadAllText(Path.Combine(_fixturesDir, "LegacyProject.user.json"));

        [Fact]
        public void ProjectImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);

            Assert.Collection(imported.DebuggerOptions.Watches,
                w1 => Assert.Equal(new Watch("v0", VariableType.Hex, false), w1),
                w2 => Assert.Equal(new Watch(" ", VariableType.Uint, false), w2),
                w3 => Assert.Equal(new Watch("v2", VariableType.Float, false), w3));

            Assert.Equal(15u, imported.DebuggerOptions.Counter);
            Assert.Equal(6u, imported.VisualizerOptions.MagicNumber);
            Assert.Equal("0-1023", imported.VisualizerColumnStyling.VisibleColumns);
            Assert.Equal("h", imported.ActiveProfile);
        }

        [Fact]
        public void GeneralImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            Assert.Equal("192.168.1.8", profile.General.RemoteMachine);
            Assert.Equal(1337, profile.General.Port);

            Assert.Collection(profile.Macros,
                m1 => Assert.Equal(new MacroItem("RadDebugDataOutputPath", "script_py_output.bin", userDefined: true), m1),
                m2 => Assert.Equal(new MacroItem("RadPpOutputPath", "$(RadPpDir)/pp_output", userDefined: true), m2),
                m3 => Assert.Equal(new MacroItem("RadDebugWorkDir", "$(RadDeployDir)", userDefined: true), m3),
                m4 => Assert.Equal(new MacroItem("RadPpDir", "$(RadDebugWorkDir)", userDefined: true), m4),
                m5 => Assert.Equal(new MacroItem("RadDisasmWorkDir", "$(RadDeployDir)", userDefined: true), m5),
                m6 => Assert.Equal(new MacroItem("RadProfileOutputPath", "$(RadProfileWorkDir)/prof_output", userDefined: true), m6),
                m7 => Assert.Equal(new MacroItem("RadProfileWorkDir", "$(RadDeployDir)", userDefined: true), m7),
                m8 => Assert.Equal(new MacroItem("RadProfileLocalCopyPath", "C:\\Users\\h\\local_prof", userDefined: true), m8));
        }

        [Fact]
        public void DebugImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            var action = profile.Actions[0];

            Assert.Equal("Debug", action.Name);
            Assert.Equal(2, action.Steps.Count);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = "python.exe",
                Arguments = "script.py -w $(RadWatches) -l $(RadBreakLine) -v \"$(RadDebugAppArgs)\" -t $(RadCounter) -p \"$(RadDebugBreakArgs)\" -f \"$(RadActiveSourceFile)\" -o \"$(RadDebugDataOutputPath)\" $(RadAppArgs)",
                WorkingDirectory = "$(RadDebugWorkDir)",
                RunAsAdmin = false,
                WaitForCompletion = true,
                TimeoutSecs = 10
            }, action.Steps[0]);
            var readDebugData = (ReadDebugDataStep)action.Steps[1];
            Assert.True(readDebugData.BinaryOutput);
            Assert.Equal(4, readDebugData.OutputOffset);

            Assert.Equal(StepEnvironment.Remote, readDebugData.OutputFile.Location);
            Assert.Equal("$(RadDebugDataOutputPath)", readDebugData.OutputFile.Path);
            Assert.True(readDebugData.OutputFile.CheckTimestamp);

            Assert.Equal(StepEnvironment.Remote, readDebugData.WatchesFile.Location);
            Assert.Equal("valid_watches", readDebugData.WatchesFile.Path);
            Assert.True(readDebugData.WatchesFile.CheckTimestamp);

            Assert.Equal(StepEnvironment.Remote, readDebugData.DispatchParamsFile.Location);
            Assert.Equal("status", readDebugData.DispatchParamsFile.Path);
            Assert.True(readDebugData.DispatchParamsFile.CheckTimestamp);
        }

        [Fact]
        public void PreprocessorImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            var action = profile.Actions[1];

            Assert.Equal("Preprocess", action.Name);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = "cpp",
                Arguments = "-DLINE=$(RadActiveSourceFileLine) \"$(RadActiveSourceFile)\" \"$(RadPpOutputPath)\"",
                WorkingDirectory = "$(RadPpDir)",
                WaitForCompletion = true
            }, action.Steps[0]);
            Assert.Equal(new CopyFileStep
            {
                Direction = FileCopyDirection.RemoteToLocal,
                SourcePath = "$(RadPpOutputPath)",
                TargetPath = "C:\\Users\\h\\local_pp",
                IfNotModified = ActionIfNotModified.Fail
            }, action.Steps[1]);
            Assert.Equal(new OpenInEditorStep
            {
                Path = "C:\\Users\\h\\local_pp",
                LineMarker = "LINE"
            }, action.Steps[2]);
        }

        [Fact]
        public void DisassemblerImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            var action = profile.Actions[2];

            Assert.Equal("Disassemble", action.Name);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = "objdump",
                Arguments = "-s --section=.text",
                WorkingDirectory = "$(RadDisasmWorkDir)",
                WaitForCompletion = true
            }, action.Steps[0]);
            Assert.Equal(new CopyFileStep
            {
                Direction = FileCopyDirection.RemoteToLocal,
                SourcePath = "$(RadDisasmWorkDir)/src.s",
                TargetPath = "C:\\Users\\h\\local_src.s",
                IfNotModified = ActionIfNotModified.Fail
            }, action.Steps[1]);
            Assert.Equal(new OpenInEditorStep
            {
                Path = "C:\\Users\\h\\local_src.s",
                LineMarker = "_start"
            }, action.Steps[2]);
        }

        [Fact]
        public void ProfilerImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            var action = profile.Actions[3];

            Assert.Equal("Profile", action.Name);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = "prof",
                Arguments = "-o $(RadProfileOutputPath)",
                WorkingDirectory = "$(RadProfileWorkDir)",
                RunAsAdmin = true,
                WaitForCompletion = true
            }, action.Steps[0]);
            Assert.Equal(new CopyFileStep
            {
                Direction = FileCopyDirection.RemoteToLocal,
                SourcePath = "$(RadProfileOutputPath)",
                TargetPath = "$(RadProfileLocalCopyPath)",
                IfNotModified = ActionIfNotModified.Fail
            }, action.Steps[1]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Local,
                Executable = "profview",
                Arguments = "$(RadProfileLocalCopyPath)",
                WaitForCompletion = false
            }, action.Steps[2]);
        }

        [Fact]
        public void BuildImportTest()
        {
            var projectOptions = JObject.Parse(_legacyJson);
            var imported = LegacyProfileImporter.ReadProjectOptions(projectOptions);
            Assert.True(imported.Profiles.TryGetValue("h", out var profile));

            var action = profile.Actions[4];

            Assert.Equal("Build", action.Name);
            Assert.Equal(new RunActionStep { Name = "Preprocess" }, action.Steps[0]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Local,
                Executable = "make",
                Arguments = "all",
                WorkingDirectory = "$(ProjectDir)",
                WaitForCompletion = true
            }, action.Steps[1]);
        }
    }
}
