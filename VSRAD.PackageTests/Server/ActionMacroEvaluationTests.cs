using Moq;
using System;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionMacroEvaluationTests
    {
        [Fact]
        public async Task RunActionStepDetectsLoopsTestAsync()
        {
            var profile = new ProfileOptions();
            var a = new ActionProfileOptions { Name = "A" };
            a.Steps.Add(new ExecuteStep());
            a.Steps.Add(new RunActionStep { Name = "A_nested" });
            var aNested = new ActionProfileOptions { Name = "A_nested" };
            aNested.Steps.Add(new CopyFileStep());
            aNested.Steps.Add(new RunActionStep { Name = "B" });
            var b = new ActionProfileOptions { Name = "B" };
            b.Steps.Add(new RunActionStep { Name = "A" });
            b.Steps.Add(new OpenInEditorStep());

            profile.Actions.Add(a);
            profile.Actions.Add(aNested);
            profile.Actions.Add(b);

            var ex = await Assert.ThrowsAsync<Exception>(async () => await a.EvaluateAsync(new Mock<IMacroEvaluator>().Object, profile));
            Assert.Equal("Encountered a circular action: A_nested -> B -> A -> A_nested", ex.Message);
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

            var ex = await Assert.ThrowsAsync<Exception>(async () => await a.EvaluateAsync(new Mock<IMacroEvaluator>().Object, profile));
            Assert.Equal("Action D not found, required by B -> C -> D", ex.Message);

            ex = await Assert.ThrowsAsync<Exception>(async () => await c.EvaluateAsync(new Mock<IMacroEvaluator>().Object, profile));
            Assert.Equal("Action D not found", ex.Message);
        }
    }
}
