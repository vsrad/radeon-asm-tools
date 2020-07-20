using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Macros
{
    public class MacroEvaluatorTests
    {
        public static readonly AsyncLazy<IReadOnlyDictionary<string, string>> EmptyRemoteEnv = GetRemoteEnv();

        private static AsyncLazy<IReadOnlyDictionary<string, string>> GetRemoteEnv(Dictionary<string, string> vars = null)
        {
            vars = vars ?? new Dictionary<string, string>();
#pragma warning disable VSTHRD012 // There's no need to provide a JoinableTaskFactory for a completed task
            return new AsyncLazy<IReadOnlyDictionary<string, string>>(() =>
                Task.FromResult((IReadOnlyDictionary<string, string>)vars));
#pragma warning restore VSTHRD012
        }

        [Fact]
        public async Task ProjectPropertiesTestAsync()
        {
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("RadDeployDir", "/home/sayaka/projects", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugScript", "$(RadDeployDir)/debug_bin.py", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugArgs", "$(RadDebugScript) --solution $(SolutionDir)", userDefined: true));

            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("SolutionDir")).ReturnsAsync("/opt/rocm/examples/h");

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new DebuggerOptions(), options);
            var result = await evaluator.EvaluateAsync("$(RadDebugArgs)");
            Assert.Equal("/home/sayaka/projects/debug_bin.py --solution /opt/rocm/examples/h", result);
        }

        [Fact]
        public async Task TransientValuesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var debuggerOptions = new DebuggerOptions(
                new List<Watch> { new Watch("a", VariableType.Hex, false), new Watch("c", VariableType.Hex, false), new Watch("tide", VariableType.Hex, false) }
            );

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, debuggerOptions, new ProfileOptions());
            var result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.Equal("a:c:tide", result);

            var transients = new MacroEvaluatorTransientValues(
                activeSourceFile: ("welcome home", 666), breakLines: new[] { 13u }, watchesOverride: new[] { "m", "c", "ride" });
            evaluator = new MacroEvaluator(props.Object, transients, EmptyRemoteEnv, debuggerOptions, new ProfileOptions());

            result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.Equal("m:c:ride", result);
            result = await evaluator.EvaluateAsync($"$({RadMacros.ActiveSourceFile}):$({RadMacros.ActiveSourceFileLine}), stop at $({RadMacros.BreakLine})");
            Assert.Equal("welcome home:666, stop at 13", result);

            transients = new MacroEvaluatorTransientValues(activeSourceFile: ("", 0), breakLines: new[] { 20u, 1u, 9u });
            evaluator = new MacroEvaluator(props.Object, transients, EmptyRemoteEnv, debuggerOptions, new ProfileOptions());
            result = await evaluator.EvaluateAsync($"-l $({RadMacros.BreakLine})");
            Assert.Equal("-l 20:1:9", result);
        }

        [Fact]
        public async Task EnvironmentVariablesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var remoteEnv = GetRemoteEnv(new Dictionary<string, string>() { { "MAMI_BREAKPOINT", "head" }, { "PATH", "/usr/bin:/root/soulgems" } });
            var localPath = Environment.GetEnvironmentVariable("PATH");

            var evaluator = new MacroEvaluator(props.Object, default, remoteEnv, new DebuggerOptions(), new ProfileOptions());
            var result = await evaluator.EvaluateAsync("Local: $ENV(PATH), Remote: $ENVR(PATH), Break at: $ENVR(MAMI_BREAKPOINT)");
            Assert.Equal($"Local: {localPath}, Remote: /usr/bin:/root/soulgems, Break at: head", result);

            result = await evaluator.EvaluateAsync("Local: $ENV(HOPEFULLY_NON_EXISTENT_VAR), Remote: $ENVR(HOPEFULLY_NON_EXISTENT_VAR)");
            Assert.Equal("Local: , Remote: ", result);
        }

        [Fact]
        public async Task EmptyMacroNameTestAsync()
        {
            var props = new Mock<IProjectProperties>(MockBehavior.Strict); // fails the test if called
            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            Assert.Equal("$()", await evaluator.EvaluateAsync("$()"));
            Assert.Equal("", await evaluator.EvaluateAsync(""));
        }

        [Fact]
        public async Task EvaluateNullStringTestAsync()
        {
            var evaluator = new MacroEvaluator(new Mock<IProjectProperties>().Object, default, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            // Null strings may come from external sources (e.g. the .user.json file) and should be treated as empty
            var value = await evaluator.EvaluateAsync(null);
            Assert.Equal("", value);
        }

        [Fact]
        public async Task RecursiveMacroEvaluationTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("RadDebugExe", "/opt/rocm/debug_exe $(RadDebugArgs)", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugArgs", "--exec $(RadDebugExe)", userDefined: true));

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new DebuggerOptions(), options);
            var exception = await Assert.ThrowsAsync<MacroEvaluationException>(() => _ = evaluator.EvaluateAsync("$(RadDebugExe)"));
            Assert.Equal("$(RadDebugExe) contains a cycle: $(RadDebugExe) -> $(RadDebugArgs) -> $(RadDebugExe)", exception.Message);
        }

        [Fact]
        public async Task RecursiveMacroInEvaluationChainTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("A", "$(A)", userDefined: true));
            options.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new DebuggerOptions(), options);
            var exception = await Assert.ThrowsAsync<MacroEvaluationException>(() => _ = evaluator.EvaluateAsync("$(B)"));
            Assert.Equal("$(B) contains a cycle: $(B) -> $(A) -> $(A)", exception.Message);
        }

        [Fact]
        public async Task ParserEdgeCaseTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("S")).ReturnsAsync("start");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("M")).ReturnsAsync("middle");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("E")).ReturnsAsync("end");

            var evaluator = new MacroEvaluator(props.Object, default, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            Assert.Equal("start middle end", await evaluator.EvaluateAsync("$(S) $(M) $(E)"));
            Assert.Equal("start $$() $( middle end", await evaluator.EvaluateAsync("$(S) $$() $( $(M) $(E)"));
            Assert.Equal("$( nested middle $( $", await evaluator.EvaluateAsync("$( nested $(M) $( $"));
        }
    }
}
