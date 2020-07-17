using Microsoft.VisualStudio.ProjectSystem.Properties;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Macros
{
    public class MacroEditContextTests
    {
        [Fact]
        public async Task RecursiveMacroInListTestAsync()
        {
            TestHelper.InitializePackageTaskFactory();

            var profile = new ProfileOptions();
            profile.Macros.Add(new MacroItem("A", "$(B)", userDefined: true));
            profile.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));
            profile.Macros.Add(new MacroItem("C", "some independent value", userDefined: true));

            var props = new Mock<IProjectProperties>();
            var evaluator = new MacroEvaluator(props.Object,
                new MacroEvaluatorTransientValues(("nofile", 0)),
                MacroEvaluatorTests.EmptyRemoteEnv,
                new DebuggerOptions(), profile);
            var context = new MacroEditContext("A", "$(B)", evaluator);

            await context.LoadPreviewListAsync(profile.Macros, props.Object, MacroEvaluatorTests.EmptyRemoteEnv);
            var displayedMacros = context.MacroListView.SourceCollection.Cast<KeyValuePair<string, string>>();

            Assert.Contains(new KeyValuePair<string, string>("$(B)", "<Unable to evaluate $(B): the macro refers to itself>"), displayedMacros);
            Assert.Contains(new KeyValuePair<string, string>("$(C)", "some independent value"), displayedMacros);
        }

        [Fact]
        public void RecursiveMacroPreviewTest()
        {
            TestHelper.InitializePackageTaskFactory();

            var profile = new ProfileOptions();
            profile.Macros.Add(new MacroItem("A", "$(B)", userDefined: true));
            profile.Macros.Add(new MacroItem("B", "$(A)", userDefined: true));
            profile.Macros.Add(new MacroItem("C", "some independent value", userDefined: true));

            var props = new Mock<IProjectProperties>();
            var evaluator = new MacroEvaluator(props.Object,
                new MacroEvaluatorTransientValues(("nofile", 0)),
                MacroEvaluatorTests.EmptyRemoteEnv,
                new DebuggerOptions(), profile);
            var context = new MacroEditContext("A", "$(B)", evaluator);

            var preview = context.EvaluatedValue;
            Assert.Equal("Unable to evaluate $(B): the macro refers to itself", preview);
        }
    }
}
