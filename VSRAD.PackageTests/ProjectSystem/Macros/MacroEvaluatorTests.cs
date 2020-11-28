using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

        private static readonly MacroEvaluatorTransientValues _emptyTransients =
            new MacroEvaluatorTransientValues(0, "", Array.Empty<uint>(), new ReadOnlyCollection<string>(Array.Empty<string>()), "", "");

        [Fact]
        public async Task ProjectPropertiesTestAsync()
        {
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("RadDeployDir", "/home/sayaka/projects", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugScript", "$(RadDeployDir)/debug_bin.py", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugArgs", "$(RadDebugScript) --solution $(SolutionDir)", userDefined: true));

            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("SolutionDir")).ReturnsAsync("/opt/rocm/examples/h");

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), options);
            var result = await evaluator.EvaluateAsync("$(RadDebugArgs)");
            Assert.True(result.TryGetResult(out var evaluated, out _));
            Assert.Equal("/home/sayaka/projects/debug_bin.py --solution /opt/rocm/examples/h", evaluated);
        }

        [Fact]
        public async Task TransientValuesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var transients = new MacroEvaluatorTransientValues(sourceLine: 666, sourcePath: @"B:\welcome\home",
                new[] { 13u }, new ReadOnlyCollection<string>(new[] { "m", "c", "ride" }));
            var evaluator = new MacroEvaluator(props.Object, transients, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());

            var result = await evaluator.GetMacroValueAsync(RadMacros.Watches);
            Assert.True(result.TryGetResult(out var evaluated, out _));
            Assert.Equal("m:c:ride", evaluated);

            result = await evaluator.EvaluateAsync($"$({RadMacros.ActiveSourceDir})\\$({RadMacros.ActiveSourceFile}):$({RadMacros.ActiveSourceFileLine}), stop at $({RadMacros.BreakLine})");
            Assert.True(result.TryGetResult(out evaluated, out _));
            Assert.Equal(@"B:\welcome\home:666, stop at 13", evaluated);

            transients = new MacroEvaluatorTransientValues(0, "nofile", new[] { 20u, 1u, 9u }, new ReadOnlyCollection<string>(new[] { "watch" }));
            evaluator = new MacroEvaluator(props.Object, transients, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            result = await evaluator.EvaluateAsync($"-l $({RadMacros.BreakLine})");
            Assert.True(result.TryGetResult(out evaluated, out _));
            Assert.Equal("-l 20:1:9", evaluated);
        }

        [Fact]
        public async Task EnvironmentVariablesTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var remoteEnv = GetRemoteEnv(new Dictionary<string, string>() { { "MAMI_BREAKPOINT", "head" }, { "PATH", "/usr/bin:/root/soulgems" } });
            var localPath = Environment.GetEnvironmentVariable("PATH");

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, remoteEnv, new DebuggerOptions(), new ProfileOptions());
            var result = await evaluator.EvaluateAsync("Local: $ENV(PATH), Remote: $ENVR(PATH), Break at: $ENVR(MAMI_BREAKPOINT)");
            Assert.True(result.TryGetResult(out var evaluated, out _));
            Assert.Equal($"Local: {localPath}, Remote: /usr/bin:/root/soulgems, Break at: head", evaluated);

            result = await evaluator.EvaluateAsync("Local: $ENV(HOPEFULLY_NON_EXISTENT_VAR), Remote: $ENVR(HOPEFULLY_NON_EXISTENT_VAR)");
            Assert.True(result.TryGetResult(out evaluated, out _));
            Assert.Equal("Local: , Remote: ", evaluated);
        }

        [Fact]
        public async Task NullRemoteEnvironmentTreatedAsLocalTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var localPath = Environment.GetEnvironmentVariable("PATH");

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, remoteEnvironment: null, new DebuggerOptions(), new ProfileOptions());
            var result = await evaluator.EvaluateAsync("Local: $ENV(PATH), Remote: $ENVR(PATH)");
            Assert.True(result.TryGetResult(out var evaluated, out _));
            Assert.Equal($"Local: {localPath}, Remote: {localPath}", evaluated);

            result = await evaluator.EvaluateAsync("Local: $ENV(HOPEFULLY_NON_EXISTENT_VAR), Remote: $ENVR(HOPEFULLY_NON_EXISTENT_VAR)");
            Assert.True(result.TryGetResult(out evaluated, out _));
            Assert.Equal("Local: , Remote: ", evaluated);
        }

        [Fact]
        public async Task EmptyMacroNameTestAsync()
        {
            var props = new Mock<IProjectProperties>(MockBehavior.Strict); // fails the test if called
            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            Assert.True((await evaluator.EvaluateAsync("$()")).TryGetResult(out var evaluated, out _));
            Assert.Equal("$()", evaluated);
            Assert.True((await evaluator.EvaluateAsync("")).TryGetResult(out evaluated, out _));
            Assert.Equal("", evaluated);
        }

        [Fact]
        public async Task EvaluateNullStringTestAsync()
        {
            var evaluator = new MacroEvaluator(new Mock<IProjectProperties>().Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            // Null strings may come from external sources (e.g. the .user.json file) and should be treated as empty
            Assert.True((await evaluator.EvaluateAsync(null)).TryGetResult(out var evaluated, out _));
            Assert.Equal("", evaluated);
        }

        [Fact]
        public async Task RecursiveMacroEvaluationTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("RadDebugExe", "/opt/rocm/debug_exe $(RadDebugArgs)", userDefined: true));
            options.Macros.Add(new MacroItem("RadDebugArgs", "--exec $(RadDebugExe)", userDefined: true));

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), options);
            Assert.False((await evaluator.EvaluateAsync("$(RadDebugExe)")).TryGetResult(out _, out var error));
            Assert.Equal("$(RadDebugExe) contains a cycle: $(RadDebugExe) -> $(RadDebugArgs) -> $(RadDebugExe)", error.Message);
        }

        [Fact]
        public async Task RecursiveMacroInEvaluationChainTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            var options = new ProfileOptions();
            options.Macros.Add(new MacroItem("A", "$(A)", userDefined: true));
            options.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), options);

            Assert.False((await evaluator.EvaluateAsync("$(B)")).TryGetResult(out _, out var error));
            Assert.Equal("$(B) contains a cycle: $(B) -> $(A) -> $(A)", error.Message);
        }

        [Fact]
        public async Task ParserEdgeCaseTestAsync()
        {
            var props = new Mock<IProjectProperties>();
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("S")).ReturnsAsync("start");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("M")).ReturnsAsync("middle");
            props.Setup((p) => p.GetEvaluatedPropertyValueAsync("E")).ReturnsAsync("end");

            var evaluator = new MacroEvaluator(props.Object, _emptyTransients, EmptyRemoteEnv, new DebuggerOptions(), new ProfileOptions());
            Assert.True((await evaluator.EvaluateAsync("$(S) $(M) $(E)")).TryGetResult(out var evaluated, out _));
            Assert.Equal("start middle end", evaluated);
            Assert.True((await evaluator.EvaluateAsync("$(S) $$() $( $(M) $(E)")).TryGetResult(out evaluated, out _));
            Assert.Equal("start $$() $( middle end", evaluated);
            Assert.True((await evaluator.EvaluateAsync("$( nested $(M) $( $")).TryGetResult(out evaluated, out _));
            Assert.Equal("$( nested middle $( $", evaluated);
        }
    }
}
