using Microsoft.VisualStudio.ProjectSystem.Properties;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.Package.ProjectSystem.Tests
{
    public class MacroEvaluatorTests
    {
        [Fact]
        public async Task ProjectPropertiesTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>();

            var options = new Options.ProjectOptions();
            options.AddProfile("Default", new Options.ProfileOptions(debugger: new Options.DebuggerProfileOptions(
                arguments: "/home/sayaka/projects/debug_bin.py --solution $(SolutionDir)")));
            project.Setup((p) => p.Options).Returns(options);
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("SolutionDir")).Returns(Task.FromResult("/opt/rocm/examples/h"));

            var evaluator = new MacroEvaluator(project.Object, props.Object, default);
            var result = await evaluator.GetMacroValueAsync(RadMacros.DebuggerArguments);
            Assert.Equal("/home/sayaka/projects/debug_bin.py --solution /opt/rocm/examples/h", result);
        }

        [Fact]
        public async Task GetMacroValueRecursiveTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>();

            var options = new Options.ProjectOptions();
            options.AddProfile("Default", new Options.ProfileOptions(debugger: new Options.DebuggerProfileOptions(
                executable: $"/opt/rocm/debug_exe $({RadMacros.DebuggerArguments})",
                arguments: $"--exec $({RadMacros.DebuggerExecutable})")));
            project.Setup((p) => p.Options).Returns(options);

            var evaluator = new MacroEvaluator(project.Object, props.Object, default);
            var exception = await Assert.ThrowsAsync<MacroEvaluationException>(
                async () => _ = await evaluator.GetMacroValueAsync(RadMacros.DebuggerExecutable));
            Assert.Equal($"Unable to evaluate $({RadMacros.DebuggerExecutable}): the macro refers to itself.", exception.Message);
        }

        [Fact]
        public async Task TransientValuesTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>();

            var options = new Options.ProjectOptions();
            options.AddProfile("Default", new Options.ProfileOptions());
            options.DebuggerOptions.Watches = new List<DebugVisualizer.Watch>
            {
                new DebugVisualizer.Watch("a", DebugVisualizer.VariableType.Hex, false),
                new DebugVisualizer.Watch("c", DebugVisualizer.VariableType.Hex, false),
                new DebugVisualizer.Watch("tide", DebugVisualizer.VariableType.Hex, false)
            };
            project.Setup((p) => p.Options).Returns(options);

            var evaluator = new MacroEvaluator(project.Object, props.Object, default);
            var result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.Equal("a:c:tide", result);

            evaluator = new MacroEvaluator(project.Object, props.Object, new MacroEvaluatorTransientValues(
                activeSourceFile: ("welcome home", 666), breakLine: 13, watchesOverride: new[] { "m", "c", "ride" }));
            Assert.Equal("m:c:ride", await evaluator.GetMacroValueAsync(RadMacros.Watches));
            Assert.Equal("welcome home:666, stop at 13", await evaluator.EvaluateAsync($"$({RadMacros.ActiveSourceFile}):$({RadMacros.ActiveSourceFileLine}), stop at $({RadMacros.BreakLine})"));
        }

        [Fact]
        public async Task ProfileOptionsOverrideTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>();

            var options = new Options.ProjectOptions();
            options.AddProfile("sayaka", new Options.ProfileOptions(general: new Options.GeneralProfileOptions("soul")));
            options.AddProfile("mami", new Options.ProfileOptions(general: new Options.GeneralProfileOptions("head")));
            options.ActiveProfile = "sayaka";
            project.Setup((p) => p.Options).Returns(options);

            var evaluator = new MacroEvaluator(project.Object, props.Object, default);
            Assert.Equal("soul", await evaluator.GetMacroValueAsync(RadMacros.DeployDirectory));
            evaluator = new MacroEvaluator(project.Object, props.Object, default, profileOptionsOverride: options.Profiles["mami"]);
            Assert.Equal("head", await evaluator.GetMacroValueAsync(RadMacros.DeployDirectory));
        }

        [Fact]
        public async Task EmptyMacroNameTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>(MockBehavior.Strict); // fails the test if called
            project.Setup((p) => p.Options).Returns(new Options.ProjectOptions());

            var evaluator = new MacroEvaluator(project.Object, props.Object, default, profileOptionsOverride: new Options.ProfileOptions());
            Assert.Equal("$()", await evaluator.EvaluateAsync("$()"));
            Assert.Equal("", await evaluator.EvaluateAsync(""));
        }

        [Fact]
        public async Task ParserEdgeCaseTestAsync()
        {
            var project = new Mock<IProject>();
            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("S")).Returns(Task.FromResult("start"));
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("M")).Returns(Task.FromResult("middle"));
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("E")).Returns(Task.FromResult("end"));
            project.Setup((p) => p.Options).Returns(new Options.ProjectOptions());

            var evaluator = new MacroEvaluator(project.Object, props.Object, default, profileOptionsOverride: new Options.ProfileOptions());
            Assert.Equal("start middle end", await evaluator.EvaluateAsync("$(S) $(M) $(E)"));
            Assert.Equal("start $$() $( middle end", await evaluator.EvaluateAsync("$(S) $$() $( $(M) $(E)"));
            Assert.Equal("$( nested middle $( $", await evaluator.EvaluateAsync("$( nested $(M) $( $"));
        }
    }
}
