using System.Globalization;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class ObsoleteFormatProfileImportTests
    {
        private static readonly string _obsoleteConfPath = TestHelper.GetFixturePath("ConfigsTest.vcxproj.conf.json");
        private static readonly string _userOptionsPath = TestHelper.GetFixturePath("ConfigsTest.vcxproj.user.json");       // does not exist
        private static readonly string _profOptionsPath = TestHelper.GetFixturePath("ConfigsTest.vcxproj.profiles.json");   // does not exist

        [Fact]
        public void ProjectImportTest()
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.Collection(imported.DebuggerOptions.Watches,
                w1 => Assert.Equal(new Watch("v0", new VariableType(VariableCategory.Hex, 32)), w1),
                w2 => Assert.Equal(new Watch(" ", new VariableType(VariableCategory.Uint, 32)), w2),
                w3 => Assert.Equal(new Watch("v2", new VariableType(VariableCategory.Float, 32)), w3));

            Assert.Equal(42u, imported.DebuggerOptions.Counter);
            Assert.Equal("0-3:16-19:32-35:48-51:64-67:80-83:96-99:112-115:128-131:144-147:160-163:176-179:192-195:208-211:224-227:240-243:256-259:272-275:288-291:304-307:320-323:336-339:352-355:368-371:384-387:400-403:416-419:432-435:448-451:464-467:480-483:496-499",
                imported.VisualizerColumnStyling.VisibleColumns);
            Assert.Equal("miho", imported.ActiveProfile);

            Assert.Equal(2, imported.Profiles.Count);
            Assert.Collection(imported.Profiles,
                p1 => Assert.Equal("hiroshi", p1.Key),
                p2 => Assert.Equal("miho", p2.Key));

            Assert.Equal("Debug", imported.Profile.MenuCommands.DebugAction);
            Assert.Equal("Profile", imported.Profile.MenuCommands.ProfileAction);
            Assert.Equal("Disassemble", imported.Profile.MenuCommands.DisassembleAction);
            Assert.Equal("Preprocess", imported.Profile.MenuCommands.PreprocessAction);
        }

        [Theory]
        [InlineData("hiroshi", "odokawa", "RadMarker1", "_odd")]
        [InlineData("miho", "shirakawa", "RadMarker2", "taxi:")]
        public void GeneralImportTest(string profileName,
                                      string userName,
                                      string markerName, string markerValue)
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.True(imported.Profiles.TryGetValue(profileName, out var profile));

            Assert.Equal("$(RadLocalWorkDir)", profile.General.LocalWorkDir);
            Assert.Equal("$(RadRemoteWorkDir)", profile.General.RemoteWorkDir);

            var userNameUpperCase = new CultureInfo("en-US", false).TextInfo.ToTitleCase(userName);

            Assert.Collection(profile.Macros,
                m1 => Assert.Equal(new MacroItem("RadLocalWorkDir", $"C:\\Users\\{userNameUpperCase}\\code", userDefined: true), m1),
                m2 => Assert.Equal(new MacroItem("RadRemoteWorkDir", $"/home/{userName}/code", userDefined: true), m2),
                m3 => Assert.Equal(new MacroItem(markerName, markerValue, userDefined: true), m3));
        }

        [Theory]
        [InlineData("hiroshi", "cargo", "run debug", 0, true, 4)]
        [InlineData("miho", "stack", "exec debug", 10, false, 2)]
        public void DebugImportTest(string profileName,
                                    string dbgExec, string dbgParams,
                                    int timeout,
                                    bool binaryData, int outputOffset)
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.True(imported.Profiles.TryGetValue(profileName, out var profile));

            var action = profile.Actions[0];

            Assert.Equal("Debug", action.Name);
            Assert.Equal(3, action.Steps.Count);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.LocalToRemote,
                SourcePath = "$(RadActiveSourceFullPath)",
                TargetPath = "src/$(RadActiveSourceFile)"
            }, action.Steps[0]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = dbgExec,
                Arguments = dbgParams,
                WorkingDirectory = "$(RadRemoteWorkDir)/src",
                RunAsAdmin = false,
                WaitForCompletion = true,
                TimeoutSecs = timeout
            }, action.Steps[1]);

            var readDebugData = (ReadDebugDataStep)action.Steps[2];

            Assert.Equal(binaryData, readDebugData.BinaryOutput);
            Assert.Equal(outputOffset, readDebugData.OutputOffset);

            Assert.Equal(StepEnvironment.Remote, readDebugData.OutputFile.Location);
            Assert.Equal("src/dbg/debug_result", readDebugData.OutputFile.Path);
            Assert.True(readDebugData.OutputFile.CheckTimestamp);

            Assert.Equal(StepEnvironment.Remote, readDebugData.WatchesFile.Location);
            Assert.Equal("src/dbg/valid_watches", readDebugData.WatchesFile.Path);
            Assert.True(readDebugData.OutputFile.CheckTimestamp);

            Assert.Equal(StepEnvironment.Remote, readDebugData.DispatchParamsFile.Location);
            Assert.Equal("src/dbg/dispatch_params", readDebugData.DispatchParamsFile.Path);
            Assert.True(readDebugData.OutputFile.CheckTimestamp);
        }

        [Theory]
        [InlineData("hiroshi", "cargo", "run pp", 0, "LINE")]
        [InlineData("miho", "stack", "exec pp", 10, "line:")]
        public void PreprocessorImportTest(string profileName,
                                           string ppExec, string ppArgs,
                                           int timeout,
                                           string lineMarker)
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.True(imported.Profiles.TryGetValue(profileName, out var profile));

            var action = profile.Actions[1];

            Assert.Equal("Preprocess", action.Name);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.LocalToRemote,
                SourcePath = "$(RadActiveSourceFullPath)",
                TargetPath = "$(RadRemoteWorkDir)/src/$(RadActiveSourceFile)"
            }, action.Steps[0]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = ppExec,
                Arguments = ppArgs,
                WorkingDirectory = "$(RadRemoteWorkDir)/src",
                RunAsAdmin = false,
                WaitForCompletion = true,
                TimeoutSecs = timeout
            }, action.Steps[1]);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.RemoteToLocal,
                SourcePath = "src/pp/pp_result",
                TargetPath = "pp_result_local"
            }, action.Steps[2]);
            Assert.Equal(new OpenInEditorStep
            {
                Path = "pp_result_local",
                LineMarker = lineMarker
            }, action.Steps[3]);
        }

        [Theory]
        [InlineData("hiroshi", "cargo", "run disasm", 0, "START")]
        [InlineData("miho", "stack", "exec disasm", 10, "_start")]
        public void DisassemblerImportTest(string profileName,
                                           string dsmExec, string dsmArgs,
                                           int timeout,
                                           string lineMarker)
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.True(imported.Profiles.TryGetValue(profileName, out var profile));

            var action = profile.Actions[2];

            Assert.Equal("Disassemble", action.Name);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.LocalToRemote,
                SourcePath = "$(RadActiveSourceFullPath)",
                TargetPath = "src/$(RadActiveSourceFile)"
            }, action.Steps[0]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = dsmExec,
                Arguments = dsmArgs,
                WorkingDirectory = "$(RadRemoteWorkDir)/src",
                RunAsAdmin = false,
                WaitForCompletion = true,
                TimeoutSecs = timeout
            }, action.Steps[1]);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.RemoteToLocal,
                SourcePath = "src/dsm/dsm_src.s",
                TargetPath = "dsm_src_local.s"
            }, action.Steps[2]);
            Assert.Equal(new OpenInEditorStep
            {
                Path = "dsm_src_local.s",
                LineMarker = lineMarker
            }, action.Steps[3]);
        }

        [Theory]
        [InlineData("hiroshi", "cargo", "run profile", 0, "profviewer.exe")]
        [InlineData("miho", "stack", "exec profile", 10, "prof_viewer.exe")]
        public void ProfilerImportTest(string profileName,
                                       string prfExec, string prfArgs,
                                       int timeout,
                                       string vwExec)
        {
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();
            var imported = ProjectOptions.Read(_userOptionsPath, _profOptionsPath, _obsoleteConfPath);
            Assert.Empty(packageErrors);

            Assert.True(imported.Profiles.TryGetValue(profileName, out var profile));

            var action = profile.Actions[3];

            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.LocalToRemote,
                SourcePath = "$(RadActiveSourceFullPath)",
                TargetPath = "src/$(RadActiveSourceFile)"
            }, action.Steps[0]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = prfExec,
                Arguments = prfArgs,
                WorkingDirectory = "src",
                RunAsAdmin = false,
                WaitForCompletion = true,
                TimeoutSecs = timeout
            }, action.Steps[1]);
            Assert.Equal(new CopyStep
            {
                Direction = CopyDirection.RemoteToLocal,
                SourcePath = "src/prf/prf_out",
                TargetPath = "prf_out_local"
            }, action.Steps[2]);
            Assert.Equal(new ExecuteStep
            {
                Environment = StepEnvironment.Local,
                Executable = vwExec,
                Arguments = "prf_out_local",
                WorkingDirectory = "",
                RunAsAdmin = false,
                WaitForCompletion = false,
                TimeoutSecs = 0
            }, action.Steps[3]);
        }
    }
}
