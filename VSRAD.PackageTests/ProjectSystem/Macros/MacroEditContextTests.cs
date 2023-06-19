using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.ProjectSystem.Macros
{
    [Collection(MockedVS.Collection)]
    public class MacroEditContextTests
    {
        [Fact]
        public async Task RecursiveMacroInListTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var profile = new ProfileOptions();
            profile.Macros.Add(new MacroItem("A", "$(B)", userDefined: true));
            profile.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));
            profile.Macros.Add(new MacroItem("C", "some independent value", userDefined: true));

            var props = new Mock<IProjectProperties>();
            var evaluator = new MacroEvaluator(props.Object,
                new MacroEvaluatorTransientValues(0, @"I:\C\ftg", Array.Empty<uint>(), new ReadOnlyCollection<string>(Array.Empty<string>())),
                MacroEvaluatorTests.EmptyRemoteEnv,
                new DebuggerOptions(), profile);
            var context = new MacroEditContext("A", "$(B)", evaluator);

            await context.LoadPreviewListAsync(profile.Macros, props.Object, MacroEvaluatorTests.EmptyRemoteEnv);
            var displayedMacros = context.MacroListView.SourceCollection.Cast<KeyValuePair<string, string>>();

            Assert.Contains(new KeyValuePair<string, string>("$(B)", "<$(B) contains a cycle: $(B) -> $(A) -> $(B)>"), displayedMacros);
            Assert.Contains(new KeyValuePair<string, string>("$(C)", "some independent value"), displayedMacros);
        }

        [Fact]
        public async Task RecursiveMacroPreviewTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var profile = new ProfileOptions();
            profile.Macros.Add(new MacroItem("A", "$(B)", userDefined: true));
            profile.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));
            profile.Macros.Add(new MacroItem("C", "some independent value", userDefined: true));

            var props = new Mock<IProjectProperties>();
            var evaluator = new MacroEvaluator(props.Object,
                new MacroEvaluatorTransientValues(0, @"I:\C\ftg", Array.Empty<uint>(), new ReadOnlyCollection<string>(Array.Empty<string>())),
                MacroEvaluatorTests.EmptyRemoteEnv,
                new DebuggerOptions(), profile);
            var context = new MacroEditContext("A", "$(B)", evaluator);

            var preview = context.EvaluatedValue;
            Assert.Equal("$(B) contains a cycle: $(B) -> $(A) -> $(B)", preview);
        }
    }
}
