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
        public async Task CopyFileStepEmptyPathsTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new CopyFileStep { SourcePath = "", TargetPath = "target" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal("No source path specified", error.Message);

            ((CopyFileStep)a.Steps[0]).SourcePath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile)).TryGetResult(out _, out error));
            Assert.Equal("The specified source path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);

            ((CopyFileStep)a.Steps[0]).SourcePath = "source";
            ((CopyFileStep)a.Steps[0]).TargetPath = "";
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal("No target path specified", error.Message);

            ((CopyFileStep)a.Steps[0]).TargetPath = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile)).TryGetResult(out _, out error));
            Assert.Equal("The specified target path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task ExecuteStepEmptyExecutableTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new ExecuteStep { Executable = "" });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal("No executable specified", error.Message);

            ((ExecuteStep)a.Steps[0]).Executable = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile)).TryGetResult(out _, out error));
            Assert.Equal("The specified executable (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task OpenInEditorStepEmptyPathTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new OpenInEditorStep { Path = "      " });
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal("No file path specified", error.Message);

            ((OpenInEditorStep)a.Steps[0]).Path = "$(MissingMacro)";
            Assert.False((await a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile)).TryGetResult(out _, out error));
            Assert.Equal("The specified file path (\"$(MissingMacro)\") evaluates to an empty string", error.Message);
        }

        [Fact]
        public async Task RunActionStepDetectsLoopsTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ExecuteStep { Executable = "-" });
            a.Steps.Add(new RunActionStep { Name = "A_nested" });
            var aNested = new ActionProfileOptions { Name = "A_nested" };
            aNested.Steps.Add(new CopyFileStep { SourcePath = "-", TargetPath = "-" });
            aNested.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new OpenInEditorStep { Path = "-" });
            b.Steps.Add(new RunActionStep { Name = "A" });

            profile.Actions.Add(a);
            profile.Actions.Add(aNested);
            profile.Actions.Add(b);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""A"" <- ""B"" <- ""A_nested"" <- ""A""", error.Title);
            Assert.Equal(@"Circular dependency between actions", error.Message);
        }

        [Fact]
        public async Task RunActionStepRefersToSelfTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "A" });
            profile.Actions.Add(a);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""A"" <- ""A""", error.Title);
            Assert.Equal(@"Circular dependency between actions", error.Message);
        }

        [Fact]
        public async Task RunActionStepReportsMissingActionsTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "C" });
            var c = new ActionProfileOptions { Name = "C" };
            c.Steps.Add(new RunActionStep { Name = "D" });

            profile.Actions.Add(a);
            profile.Actions.Add(b);
            profile.Actions.Add(c);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""C"" <- ""B"" <- ""A""", error.Title);
            Assert.Equal(@"Action ""D"" is not found", error.Message);

            Assert.False((await c.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Run Action step failed in ""C""", error.Title);
            Assert.Equal(@"Action ""D"" is not found", error.Message);
        }

        [Fact]
        public async Task RunActionNoActionTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "" });
            profile.Actions.Add(a);
            profile.Actions.Add(b);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Run Action step failed in ""B"" <- ""A""", error.Title);
            Assert.Equal(@"No action specified", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Run Action step failed in ""B""", error.Title);
            Assert.Equal(@"No action specified", error.Message);
        }

        [Fact]
        public async Task RunActionUnconfiguredReferenceTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "C" });
            var c = new ActionProfileOptions { Name = "C" };
            c.Steps.Add(new CopyFileStep());
            profile.Actions.Add(a);
            profile.Actions.Add(b);
            profile.Actions.Add(c);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Copy File step failed in ""C"" <- ""B"" <- ""A""", error.Title);
            Assert.Equal(@"No source path specified", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Copy File step failed in ""C"" <- ""B""", error.Title);
            Assert.Equal(@"No source path specified", error.Message);
        }

        [Fact]
        public async Task ReadDebugDataEmptyOutputPathTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new ReadDebugDataStep());
            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Read Debug Data step failed in ""A""", error.Title);
            Assert.Equal("Debug data path is not specified", error.Message);
        }

        [Fact]
        public async Task RunActionsLocallyTestAsync()
        {
            var profile = new ProfileOptions();
            profile.General.RunActionsLocally = true;

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

            profile.General.RunActionsLocally = false;
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out var result, out _));
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

            profile.General.RunActionsLocally = true;
            Assert.True((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out result, out _));
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
