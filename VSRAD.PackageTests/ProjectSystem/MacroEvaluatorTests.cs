using Microsoft.VisualStudio.ProjectSystem.Properties;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.Package.ProjectSystem.Tests
{
    public class MacroEvaluatorTests
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyRemoteEnv = new Dictionary<string, string>();

        [Fact]
        public async Task ProjectPropertiesTestAsync()
        {
            var profileOptions = new Options.ProfileOptions(debugger: new Options.DebuggerProfileOptions(
                arguments: "/home/sayaka/projects/debug_bin.py --solution $(SolutionDir)"));

            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("SolutionDir")).ReturnsAsync("/opt/rocm/examples/h");

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new Options.DebuggerOptions(), profileOptions);
            var result = await evaluator.GetMacroValueAsync(RadMacros.DebuggerArguments);
            Assert.Equal("/home/sayaka/projects/debug_bin.py --solution /opt/rocm/examples/h", result);
        }

        [Fact]
        public async Task TransientValuesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var debuggerOptions = new Options.DebuggerOptions(
                new List<Watch> { new Watch("a", VariableType.Hex, false), new Watch("c", VariableType.Hex, false), new Watch("tide", VariableType.Hex, false) }
            );

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, debuggerOptions, new Options.ProfileOptions());
            var result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.Equal("a:c:tide", result);

            var transients = new MacroEvaluatorTransientValues(
                activeSourceFile: ("welcome home", 666), breakLine: 13, watchesOverride: new[] { "m", "c", "ride" });
            evaluator = new MacroEvaluator(props.Object, transients, EmptyRemoteEnv, debuggerOptions, new Options.ProfileOptions());

            result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.Equal("m:c:ride", result);
            result = await evaluator.EvaluateAsync($"$({RadMacros.ActiveSourceFile}):$({RadMacros.ActiveSourceFileLine}), stop at $({RadMacros.BreakLine})");
            Assert.Equal("welcome home:666, stop at 13", result);
        }

        [Fact]
        public async Task EnvironmentVariablesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var remoteEnv = new Dictionary<string, string>() { { "MAMI_BREAKPOINT", "head" }, { "PATH", "/usr/bin:/root/soulgems" } };
            var localPath = Environment.GetEnvironmentVariable("PATH");

            var evaluator = new MacroEvaluator(props.Object, default, remoteEnv, new Options.DebuggerOptions(), new Options.ProfileOptions());
            var result = await evaluator.EvaluateAsync("Local: $ENV(PATH), Remote: $ENVR(PATH), Break at: $ENVR(MAMI_BREAKPOINT)");
            Assert.Equal($"Local: {localPath}, Remote: /usr/bin:/root/soulgems, Break at: head", result);

            result = await evaluator.EvaluateAsync("Local: $ENV(HOPEFULLY_NON_EXISTENT_VAR), Remote: $ENVR(HOPEFULLY_NON_EXISTENT_VAR)");
            Assert.Equal("Local: , Remote: ", result);
        }

        [Fact]
        public async Task EmptyMacroNameTestAsync()
        {
            var props = new Mock<IProjectProperties>(MockBehavior.Strict); // fails the test if called
            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new Options.DebuggerOptions(), new Options.ProfileOptions());
            Assert.Equal("$()", await evaluator.EvaluateAsync("$()"));
            Assert.Equal("", await evaluator.EvaluateAsync(""));
        }

        [Fact]
        public async Task RecursiveMacroHandlingTestAsync()
        {
            var props = new Mock<IProjectProperties>();

            var profileOptions = new Options.ProfileOptions(debugger: new Options.DebuggerProfileOptions(
                executable: $"/opt/rocm/debug_exe $({RadMacros.DebuggerArguments})",
                arguments: $"--exec $({RadMacros.DebuggerExecutable})"));

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new Options.DebuggerOptions(), profileOptions);
            var exception = await Assert.ThrowsAsync<MacroEvaluationException>(
                () => _ = evaluator.GetMacroValueAsync(RadMacros.DebuggerExecutable));
            Assert.Equal($"Unable to evaluate $({RadMacros.DebuggerExecutable}): the macro refers to itself.", exception.Message);
        }

        [Fact]
        public async Task ParserEdgeCaseTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("S")).ReturnsAsync("start");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("M")).ReturnsAsync("middle");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("E")).ReturnsAsync("end");

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new Options.DebuggerOptions(), new Options.ProfileOptions());
            Assert.Equal("start middle end", await evaluator.EvaluateAsync("$(S) $(M) $(E)"));
            Assert.Equal("start $$() $( middle end", await evaluator.EvaluateAsync("$(S) $$() $( $(M) $(E)"));
            Assert.Equal("$( nested middle $( $", await evaluator.EvaluateAsync("$( nested $(M) $( $"));
        }
    }
}
