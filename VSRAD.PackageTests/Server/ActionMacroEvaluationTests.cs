using Moq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionMacroEvaluationTests
    {
        private static IMacroEvaluator MakeIdentityEvaluator()
        {
            var mock = new Mock<IMacroEvaluator>();
            mock.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult<Result<string>>(s));
            return mock.Object;
        }

        private static IMacroEvaluator MakeEvaluator(string unevaluated, string result)
        {
            var mock = new Mock<IMacroEvaluator>();
            mock.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult<Result<string>>(s == unevaluated ? result : unevaluated));
            return mock.Object;
        }

        [Fact]
        public async Task CopyFileStepsPathResolutionTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };

            var transientsLnx = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });
            var transientsWin = new ActionEvaluationTransients(@"C:\Local", @"C:\Remote\", false, System.Runtime.InteropServices.OSPlatform.Windows, new[] { a });

            a.Steps.Add(new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "linux/rel", TargetPath = @"windows\rel" });
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsLnx)).TryGetResult(out var evaluated, out _));
            Assert.Equal("/remote/linux/rel", ((CopyStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"C:\Local\windows\rel", ((CopyStep)evaluated.Steps[0]).TargetPath);

            a.Steps[0] = new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/linux/abs/path", TargetPath = @"D:\Windows\Abs\Path" };
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsLnx)).TryGetResult(out evaluated, out _));
            Assert.Equal("/linux/abs/path", ((CopyStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"D:\Windows\Abs\Path", ((CopyStep)evaluated.Steps[0]).TargetPath);

            a.Steps[0] = new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = @"windows\rel", TargetPath = @"windows\rel" };
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsWin)).TryGetResult(out evaluated, out _));
            Assert.Equal(@"C:\Local\windows\rel", ((CopyStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"C:\Remote\windows\rel", ((CopyStep)evaluated.Steps[0]).TargetPath);

            // Invalid paths

            a.Steps[0] = new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = "windows>_<", TargetPath = "target" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsWin)).TryGetResult(out _, out var error));
            Assert.Equal(@"Path contains illegal characters: ""windows>_<""" + "\r\n" + @"Working directory: ""C:\Local""", error.Message);

            a.Steps[0] = new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = "source", TargetPath = "target?|" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsWin)).TryGetResult(out _, out error));
            Assert.Equal(@"Path contains illegal characters: ""target?|""" + "\r\n" + @"Working directory: ""C:\Remote\""", error.Message);

            var transientsInvalidPath = new ActionEvaluationTransients("", @"C:|Remote", false, System.Runtime.InteropServices.OSPlatform.Windows, new[] { a });

            a.Steps[0] = new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = "source", TargetPath = "target" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsInvalidPath)).TryGetResult(out _, out error));
            Assert.Equal(@"Path contains illegal characters: ""target""" + "\r\n" + @"Working directory: ""C:|Remote""", error.Message);
        }

        [Fact]
        public async Task CopyFileStepEmptyPathsTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            a.Steps.Add(new CopyStep { SourcePath = "", TargetPath = "target" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal("No source path specified", error.Message);

            ((CopyStep)a.Steps[0]).SourcePath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), transients)).TryGetResult(out _, out error));
            Assert.Equal("The specified source path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);

            ((CopyStep)a.Steps[0]).SourcePath = "source";
            ((CopyStep)a.Steps[0]).TargetPath = "";
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out error));
            Assert.Equal("No target path specified", error.Message);

            ((CopyStep)a.Steps[0]).TargetPath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), transients)).TryGetResult(out _, out error));
            Assert.Equal("The specified target path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task ExecuteStepEmptyExecutableTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            a.Steps.Add(new ExecuteStep { Executable = "" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal("No executable specified", error.Message);

            ((ExecuteStep)a.Steps[0]).Executable = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), transients)).TryGetResult(out _, out error));
            Assert.Equal("The specified executable (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task OpenInEditorStepEmptyPathTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            a.Steps.Add(new OpenInEditorStep { Path = "      " });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal("No file path specified", error.Message);

            ((OpenInEditorStep)a.Steps[0]).Path = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), transients)).TryGetResult(out _, out error));
            Assert.Equal("The specified file path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task RunActionStepDetectsLoopsTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ExecuteStep { Executable = "-" });
            a.Steps.Add(new RunActionStep { Name = "A_nested" });
            var aNested = new ActionProfileOptions { Name = "A_nested" };
            aNested.Steps.Add(new CopyStep { SourcePath = "-", TargetPath = "-" });
            aNested.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new OpenInEditorStep { Path = "-" });
            b.Steps.Add(new RunActionStep { Name = "A" });

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a, aNested, b });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""A"" <- ""B"" <- ""A_nested"" <- ""A""", error.Title);
            Assert.Equal(@"Circular dependency between actions", error.Message);
        }

        [Fact]
        public async Task RunActionStepRefersToSelfTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "A" });

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""A"" <- ""A""", error.Title);
            Assert.Equal(@"Circular dependency between actions", error.Message);
        }

        [Fact]
        public async Task RunActionStepReportsMissingActionsTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "C" });
            var c = new ActionProfileOptions { Name = "C" };
            c.Steps.Add(new RunActionStep { Name = "D" });

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a, b, c });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""C"" <- ""B"" <- ""A""", error.Title);
            Assert.Equal(@"Action ""D"" is not found", error.Message);

            Assert.False((await c.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out error));
            Assert.Equal(@"Run Action step failed in ""C""", error.Title);
            Assert.Equal(@"Action ""D"" is not found", error.Message);
        }

        [Fact]
        public async Task RunActionNoActionTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "" });

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a, b });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""B"" <- ""A""", error.Title);
            Assert.Equal(@"No action specified", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out error));
            Assert.Equal(@"Run Action step failed in ""B""", error.Title);
            Assert.Equal(@"No action specified", error.Message);
        }

        [Fact]
        public async Task RunActionUnconfiguredReferenceTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "C" });
            var c = new ActionProfileOptions { Name = "C" };
            c.Steps.Add(new CopyStep());

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a, b, c });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Copy step failed in ""C"" <- ""B"" <- ""A""", error.Title);
            Assert.Equal(@"No source path specified", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out error));
            Assert.Equal(@"Copy step failed in ""C"" <- ""B""", error.Title);
            Assert.Equal(@"No source path specified", error.Message);
        }

        [Fact]
        public async Task ReadDebugDataEmptyOutputPathTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ReadDebugDataStep());

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out _, out var error));
            Assert.Equal(@"Read Debug Data step failed in ""A""", error.Title);
            Assert.Equal("Debug data path is not specified", error.Message);
        }

        [Fact]
        public async Task RunActionsLocallyTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = "lr-copy-source", TargetPath = "lr-copy-target" });
            a.Steps.Add(new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "rl-copy-source", TargetPath = "rl-copy-target" });
            a.Steps.Add(new CopyStep { Direction = CopyDirection.LocalToLocal, SourcePath = "ll-copy-source", TargetPath = "ll-copy-target" });
            a.Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "remote-executable" });
            a.Steps.Add(new ExecuteStep { Environment = StepEnvironment.Local, Executable = "local-executable" });
            a.Steps.Add(new ReadDebugDataStep(
                outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-output", CheckTimestamp = true },
                watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-watches", CheckTimestamp = false },
                dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-status", CheckTimestamp = false },
                binaryOutput: true, outputOffset: 0, magicNumber: null));

            var transients = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: false, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });

            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), transients)).TryGetResult(out var result, out _));
            {
                var copyFileLR = (CopyStep)result.Steps[0];
                Assert.Equal(CopyDirection.LocalToRemote, copyFileLR.Direction);
            }
            {
                var copyFileRL = (CopyStep)result.Steps[1]; /* RemoteToLocal becomes LocalToLocal */
                Assert.Equal(CopyDirection.RemoteToLocal, copyFileRL.Direction);
            }
            {
                var copyFileLL = (CopyStep)result.Steps[2]; /* LocalToLocal remains LocalToLocal */
                Assert.Equal(CopyDirection.LocalToLocal, copyFileLL.Direction);
            }
            {
                var executeR = (ExecuteStep)result.Steps[3];
                Assert.Equal(StepEnvironment.Remote, executeR.Environment);
            }
            {
                var executeL = (ExecuteStep)result.Steps[4];
                Assert.Equal(StepEnvironment.Local, executeL.Environment);
            }
            {
                var readDebugData = (ReadDebugDataStep)result.Steps[5];
                Assert.Equal(StepEnvironment.Remote, readDebugData.OutputFile.Location);
                Assert.Equal(StepEnvironment.Remote, readDebugData.DispatchParamsFile.Location);
                Assert.Equal(StepEnvironment.Remote, readDebugData.WatchesFile.Location);
            }

            var transientsLocal = new ActionEvaluationTransients(@"C:\Local", "/remote", runActionsLocally: true, System.Runtime.InteropServices.OSPlatform.Linux, new[] { a });
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), transientsLocal)).TryGetResult(out result, out _));
            {
                var copyFileLR = (CopyStep)result.Steps[0]; /* LocalToRemote becomes LocalToLocal */
                Assert.Equal(CopyDirection.LocalToLocal, copyFileLR.Direction);
            }
            {
                var copyFileRL = (CopyStep)result.Steps[1]; /* RemoteToLocal becomes LocalToLocal */
                Assert.Equal(CopyDirection.LocalToLocal, copyFileRL.Direction);
            }
            {
                var copyFileLL = (CopyStep)result.Steps[2]; /* LocalToLocal remains LocalToLocal */
                Assert.Equal(CopyDirection.LocalToLocal, copyFileLL.Direction);
            }
            {
                var executeR = (ExecuteStep)result.Steps[3];
                Assert.Equal(StepEnvironment.Local, executeR.Environment);
            }
            {
                var executeL = (ExecuteStep)result.Steps[4];
                Assert.Equal(StepEnvironment.Local, executeL.Environment);
            }
            {
                var readDebugData = (ReadDebugDataStep)result.Steps[5];
                Assert.Equal(StepEnvironment.Local, readDebugData.OutputFile.Location);
                Assert.Equal(StepEnvironment.Local, readDebugData.DispatchParamsFile.Location);
                Assert.Equal(StepEnvironment.Local, readDebugData.WatchesFile.Location);
            }
        }
    }
}
