using Moq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
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

            var envLinux = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });
            var envWin = new ActionEvaluationEnvironment(@"C:\Local", @"C:\Remote\", false, new CapabilityInfo(default, ServerPlatform.Windows, default), new[] { a });

            a.Steps.Add(new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "linux/rel", TargetPath = @"windows\rel" });
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), envLinux)).TryGetResult(out var evaluated, out _));
            Assert.Equal("/remote/linux/rel", ((CopyFileStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"C:\Local\windows\rel", ((CopyFileStep)evaluated.Steps[0]).TargetPath);

            a.Steps[0] = new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/linux/abs/path", TargetPath = @"D:\Windows\Abs\Path" };
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), envLinux)).TryGetResult(out evaluated, out _));
            Assert.Equal("/linux/abs/path", ((CopyFileStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"D:\Windows\Abs\Path", ((CopyFileStep)evaluated.Steps[0]).TargetPath);

            a.Steps[0] = new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = @"windows\rel", TargetPath = @"windows\rel" };
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), envWin)).TryGetResult(out evaluated, out _));
            Assert.Equal(@"C:\Local\windows\rel", ((CopyFileStep)evaluated.Steps[0]).SourcePath);
            Assert.Equal(@"C:\Remote\windows\rel", ((CopyFileStep)evaluated.Steps[0]).TargetPath);

            // Invalid paths

            a.Steps[0] = new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = "windows>_<", TargetPath = "target" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), envWin)).TryGetResult(out _, out var error));
            Assert.Equal(@"Path contains illegal characters: ""windows>_<""" + "\r\n" + @"Working directory: ""C:\Local""", error.Message);

            a.Steps[0] = new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = "source", TargetPath = "target?|" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), envWin)).TryGetResult(out _, out error));
            Assert.Equal(@"Path contains illegal characters: ""target?|""" + "\r\n" + @"Working directory: ""C:\Remote\""", error.Message);

            var envInvalidPath = new ActionEvaluationEnvironment("", @"C:|Remote", false, new CapabilityInfo(default, ServerPlatform.Windows, default), new[] { a });

            a.Steps[0] = new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = "source", TargetPath = "target" };
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), envInvalidPath)).TryGetResult(out _, out error));
            Assert.Equal(@"Path contains illegal characters: ""target""" + "\r\n" + @"Working directory: ""C:|Remote""", error.Message);
        }

        [Fact]
        public async Task CopyFileStepEmptyPathsTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            a.Steps.Add(new CopyFileStep { SourcePath = "", TargetPath = "target" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal("No source path specified", error.Message);

            ((CopyFileStep)a.Steps[0]).SourcePath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), env)).TryGetResult(out _, out error));
            Assert.Equal("The specified source path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);

            ((CopyFileStep)a.Steps[0]).SourcePath = "source";
            ((CopyFileStep)a.Steps[0]).TargetPath = "";
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out error));
            Assert.Equal("No target path specified", error.Message);

            ((CopyFileStep)a.Steps[0]).TargetPath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), env)).TryGetResult(out _, out error));
            Assert.Equal("The specified target path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task ExecuteStepEmptyExecutableTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            a.Steps.Add(new ExecuteStep { Executable = "" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal("No executable specified", error.Message);

            ((ExecuteStep)a.Steps[0]).Executable = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), env)).TryGetResult(out _, out error));
            Assert.Equal("The specified executable (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task OpenInEditorStepEmptyPathTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            a.Steps.Add(new OpenInEditorStep { Path = "      " });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal("No file path specified", error.Message);

            ((OpenInEditorStep)a.Steps[0]).Path = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), env)).TryGetResult(out _, out error));
            Assert.Equal("The specified file path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task RunActionStepDetectsLoopsTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ExecuteStep { Executable = "-" });
            a.Steps.Add(new RunActionStep { Name = "A_nested" });
            var aNested = new ActionProfileOptions { Name = "A_nested" };
            aNested.Steps.Add(new CopyFileStep { SourcePath = "-", TargetPath = "-" });
            aNested.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new OpenInEditorStep { Path = "-" });
            b.Steps.Add(new RunActionStep { Name = "A" });

            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a, aNested, b });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Encountered a circular dependency: ""A"" -> ""A_nested"" -> ""B"" -> ""A""", error.Message);
        }

        [Fact]
        public async Task RunActionStepRefersToSelfTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "A" });
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Encountered a circular dependency: ""A"" -> ""A""", error.Message);
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

            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a, b, c });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Action ""A"" could not be run due to a misconfigured Run Action step", error.Title);
            Assert.Equal(@"Action ""D"" is not found, required by ""A"" -> ""B"" -> ""C""", error.Message);

            Assert.False((await c.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""C"" could not be run due to a misconfigured Run Action step", error.Title);
            Assert.Equal(@"Action ""D"" is not found", error.Message);
        }

        [Fact]
        public async Task RunActionNoActionTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "" });
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a, b });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Action ""A"" could not be run due to a misconfigured Run Action step", error.Title);
            Assert.Equal(@"No action specified, required by ""A"" -> ""B""", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""B"" could not be run due to a misconfigured Run Action step", error.Title);
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
            c.Steps.Add(new CopyFileStep());
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a, b, c });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Action ""C"" is misconfigured, required by ""A"" -> ""B""", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""C"" is misconfigured", error.Message);
        }

        [Fact]
        public async Task ReadDebugDataEmptyOutputPathTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ReadDebugDataStep());
            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out _, out var error));
            Assert.Equal(@"Action ""A"" could not be run due to a misconfigured Read Debug Data step", error.Title);
            Assert.Equal("Debug data path is not specified", error.Message);
        }

        [Fact]
        public async Task RunActionsLocallyTestAsync()
        {
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = "lr-copy-source", TargetPath = "lr-copy-target" });
            a.Steps.Add(new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "rl-copy-source", TargetPath = "rl-copy-target" });
            a.Steps.Add(new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = "ll-copy-source", TargetPath = "ll-copy-target" });
            a.Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "remote-executable" });
            a.Steps.Add(new ExecuteStep { Environment = StepEnvironment.Local, Executable = "local-executable" });
            a.Steps.Add(new ReadDebugDataStep(
                outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-output", CheckTimestamp = true },
                watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-watches", CheckTimestamp = false },
                dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote-status", CheckTimestamp = false },
                binaryOutput: true, outputOffset: 0));

            var env = new ActionEvaluationEnvironment(@"C:\Local", "/remote", runActionsLocally: false, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });

            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), env)).TryGetResult(out var result, out _));
            {
                var copyFileLR = (CopyFileStep)result.Steps[0];
                Assert.Equal(FileCopyDirection.LocalToRemote, copyFileLR.Direction);
            }
            {
                var copyFileRL = (CopyFileStep)result.Steps[1]; /* RemoteToLocal becomes LocalToLocal */
                Assert.Equal(FileCopyDirection.RemoteToLocal, copyFileRL.Direction);
            }
            {
                var copyFileLL = (CopyFileStep)result.Steps[2]; /* LocalToLocal remains LocalToLocal */
                Assert.Equal(FileCopyDirection.LocalToLocal, copyFileLL.Direction);
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

            var envLocal = new ActionEvaluationEnvironment(@"C:\Local", "/remote", runActionsLocally: true, new CapabilityInfo(default, ServerPlatform.Linux, default), new[] { a });
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), envLocal)).TryGetResult(out result, out _));
            {
                var copyFileLR = (CopyFileStep)result.Steps[0]; /* LocalToRemote becomes LocalToLocal */
                Assert.Equal(FileCopyDirection.LocalToLocal, copyFileLR.Direction);
            }
            {
                var copyFileRL = (CopyFileStep)result.Steps[1]; /* RemoteToLocal becomes LocalToLocal */
                Assert.Equal(FileCopyDirection.LocalToLocal, copyFileRL.Direction);
            }
            {
                var copyFileLL = (CopyFileStep)result.Steps[2]; /* LocalToLocal remains LocalToLocal */
                Assert.Equal(FileCopyDirection.LocalToLocal, copyFileLL.Direction);
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
