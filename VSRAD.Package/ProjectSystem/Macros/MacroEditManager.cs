using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class MacroEditManager
    {
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;

        [ImportingConstructor]
        public MacroEditManager(UnconfiguredProject unconfiguredProject, IProject project, ICommunicationChannel channel)
        {
            _unconfiguredProject = unconfiguredProject;
            _project = project;
            _channel = channel;
        }

        public async Task<string> EditAsync(string macroName, string currentValue, Options.ProfileOptions profileOptions)
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            var propertiesProvider = configuredProject.GetService<IProjectPropertiesProvider>("ProjectPropertiesProvider");
            var projectProperties = propertiesProvider.GetCommonProperties();

            var remoteEnv = await _channel.GetRemoteEnvironmentAsync().ConfigureAwait(false);
            var transients = new MacroEvaluatorTransientValues(activeSourceFile: ("<current source file>", 0));

            var evaluator = new MacroEvaluator(_project, projectProperties, transients, remoteEnv, profileOptions);

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var editor = new MacroEditor(macroName, currentValue, evaluator);
            editor.LoadPreviewListInBackground(projectProperties, _channel);

            var editorWindow = new MacroEditorWindow(editor);
            editorWindow.ShowDialog();

            return editor.MacroValue;
        }
    }
}
