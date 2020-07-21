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

            a.Steps.Add(new OpenInEditorStep { Path = "" });
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
            Assert.Equal(@"Encountered a circular dependency: ""A"" -> ""A_nested"" -> ""B"" -> ""A""", error.Message);
        }

        [Fact]
        public async Task RunActionStepRefersToSelfTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "A" });
            profile.Actions.Add(a);

            Assert.False((await a.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out var error));
            Assert.Equal(@"Encountered a circular dependency: ""A"" -> ""A""", error.Message);
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
            Assert.Equal(@"Action ""A"" could not be run due to a misconfigured Run Action step", error.Title);
            Assert.Equal(@"Action ""D"" is not found, required by ""A"" -> ""B"" -> ""C""", error.Message);

            Assert.False((await c.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""C"" could not be run due to a misconfigured Run Action step", error.Title);
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
            Assert.Equal(@"Action ""A"" could not be run due to a misconfigured Run Action step", error.Title);
            Assert.Equal(@"No action specified, required by ""A"" -> ""B""", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""B"" could not be run due to a misconfigured Run Action step", error.Title);
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
            Assert.Equal(@"Action ""C"" is misconfigured, required by ""A"" -> ""B""", error.Message);

            Assert.False((await b.EvaluateAsync(MakeIdentityEvaluator(), profile)).TryGetResult(out _, out error));
            Assert.Equal(@"Action ""C"" is misconfigured", error.Message);
        }
    }
}
