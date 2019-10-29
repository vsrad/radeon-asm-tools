using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class MacroEditor
    {
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IProject _project;

        [ImportingConstructor]
        public MacroEditor(UnconfiguredProject unconfiguredProject, IProject project)
        {
            _unconfiguredProject = unconfiguredProject;
            _project = project;
        }

        public async Task<string> EditAsync(string macroName, string currentValue, Options.ProfileOptions profileOptions)
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            var propertiesProvider = configuredProject.GetService<IProjectPropertiesProvider>("ProjectPropertiesProvider");
            var projectProperties = propertiesProvider.GetCommonProperties();

            var evaluator = new MacroEvaluator(_project, projectProperties,
                values: new MacroEvaluatorTransientValues(activeSourceFile: ("<current source file>", 0)),
                profileOptionsOverride: profileOptions);
            var environmentMacros = await GetEnvironmentMacrosAsync(projectProperties, evaluator);

            string evaluatorDelegate(string value) => VSPackage.TaskFactory.Run(() => evaluator.EvaluateAsync(value));

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var editorWindow = new MacroEditorWindow(macroName, currentValue, environmentMacros, evaluatorDelegate);
            editorWindow.ShowDialog();

            return editorWindow.Value;
        }

        private async Task<Dictionary<string, string>> GetEnvironmentMacrosAsync(IProjectProperties properties, MacroEvaluator evaluator)
        {
            var macros = new Dictionary<string, string>();

            var vsMacroNames = await properties.GetPropertyNamesAsync();
            var radMacroNames = typeof(RadMacros).GetConstantValues<string>();

            foreach (var macroName in vsMacroNames.Union(radMacroNames))
                macros["$(" + macroName + ")"] = await evaluator.GetMacroValueAsync(macroName);

            return macros;
        }
    }
}
