using Moq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionMacroEvaluationTests
    {
        private static IMacroEvaluator MakeIdentityEvaluator()
        {
            var mock = new Mock<IMacroEvaluator>();
            mock.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult(s));
            return mock.Object;
        }

        private static IMacroEvaluator MakeEvaluator(string unevaluated, string result)
        {
            var mock = new Mock<IMacroEvaluator>();
            mock.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult(s == unevaluated ? result : unevaluated));
            return mock.Object;
        }

        [Fact]
        public async Task CopyFileStepEmptyPathsTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new CopyFileStep { SourcePath = "", TargetPath = "target" });
            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("No source path specified for Copy File step", ex.Description);
            ((CopyFileStep)a.Steps[0]).SourcePath = "$(MissingMacro)";
            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile));
            Assert.Equal("The source path specified for Copy File step (\"$(MissingMacro)\") evaluates to an empty string", ex.Description);

            ((CopyFileStep)a.Steps[0]).SourcePath = "source";
            ((CopyFileStep)a.Steps[0]).TargetPath = "";
            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("No target path specified for Copy File step", ex.Description);
            ((CopyFileStep)a.Steps[0]).TargetPath = "$(MissingMacro)";
            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile));
            Assert.Equal("The target path specified for Copy File step (\"$(MissingMacro)\") evaluates to an empty string", ex.Description);
        }

        [Fact]
        public async Task ExecuteStepEmptyExecutableTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new ExecuteStep { Executable = "" });
            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("No executable specified for Execute step", ex.Description);
            ((ExecuteStep)a.Steps[0]).Executable = "$(MissingMacro)";
            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile));
            Assert.Equal("The executable specified for Execute step (\"$(MissingMacro)\") evaluates to an empty string", ex.Description);
        }

        [Fact]
        public async Task OpenInEditorStepEmptyPathTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };

            a.Steps.Add(new OpenInEditorStep { Path = "" });
            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("No path specified for Open in Editor step", ex.Description);
            ((OpenInEditorStep)a.Steps[0]).Path = "$(MissingMacro)";
            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeEvaluator("$(MissingMacro)", ""), profile));
            Assert.Equal("The path specified for Open in Editor step (\"$(MissingMacro)\") evaluates to an empty string", ex.Description);
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

            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("A", ex.SourceAction);
            Assert.Equal(@"Encountered a circular dependency between Run Action steps: ""A"" -> ""A_nested"" -> ""B"" -> ""A""", ex.Description);
        }

        [Fact]
        public async Task RunActionStepRefersToSelfTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "A" });
            profile.Actions.Add(a);
            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("A", ex.SourceAction);
            Assert.Equal(@"Encountered a circular dependency between Run Action steps: ""A"" -> ""A""", ex.Description);
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

            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("A", ex.SourceAction);
            Assert.Equal(@"Action ""D"" is specified in a Run Action step but was not found, required by ""A"" -> ""B"" -> ""C""", ex.Description);

            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => c.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("C", ex.SourceAction);
            Assert.Equal(@"Action ""D"" is specified in a Run Action step but was not found, required by ""C""", ex.Description);
        }

        [Fact]
        public async Task RunActionUnconfiguredTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "" });
            profile.Actions.Add(a);
            profile.Actions.Add(b);

            var ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => a.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("A", ex.SourceAction);
            Assert.Equal(@"No action specified for Run Action step, required by ""A"" -> ""B""", ex.Description);

            ex = await Assert.ThrowsAsync<ActionEvaluationException>(() => b.EvaluateAsync(MakeIdentityEvaluator(), profile));
            Assert.Equal("B", ex.SourceAction);
            Assert.Equal(@"No action specified for Run Action step, required by ""B""", ex.Description);
        }
    }
}
