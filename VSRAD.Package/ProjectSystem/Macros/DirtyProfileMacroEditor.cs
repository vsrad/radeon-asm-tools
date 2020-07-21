using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class DirtyProfileMacroEditor
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly Options.ProfileOptions _dirtyProfile;

        public DirtyProfileMacroEditor(IProject project, ICommunicationChannel channel, Options.ProfileOptions dirtyProfile)
        {
            _project = project;
            _channel = channel;
            _dirtyProfile = dirtyProfile;
        }

        public Task<IActionStep> EvaluateStepAsync(IActionStep step, string sourceAction)
        {
            var transients = new MacroEvaluatorTransientValues(sourceLine: 0, sourcePath: "<...>", sourceDir: "<...>", sourceFile: "<...>");
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            return step.EvaluateAsync(evaluator, _dirtyProfile, sourceAction);
        }

        public async Task EditObjectPropertyAsync(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            var oldValue = (string)property.GetValue(target);
            var newValue = await EditAsync(propertyName, oldValue);
            property.SetValue(target, newValue);
        }

        public async Task<string> EditAsync(string macroName, string currentValue)
        {
            var transients = new MacroEvaluatorTransientValues(sourceLine: 0,
                sourcePath: "<current source full path>",
                sourceDir: "<current source dir name>",
                sourceFile: "<current source file name>");

            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var editor = new MacroEditContext(macroName, currentValue, evaluator);
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() =>
                editor.LoadPreviewListAsync(_dirtyProfile.Macros, ProjectProperties, RemoteEnvironment));

            var editorWindow = new MacroEditorWindow(editor)
            {
                Owner = Application.Current.MainWindow,
                ShowInTaskbar = false
            };
            editorWindow.ShowDialog();

            return editor.MacroValue;
        }

        private IProjectProperties _projectProperties;
        private IProjectProperties ProjectProperties
        {
            get
            {
                if (_projectProperties == null)
                    _projectProperties = _project.GetProjectProperties();
                return _projectProperties;
            }
        }

        private AsyncLazy<IReadOnlyDictionary<string, string>> _remoteEnv;
        private AsyncLazy<IReadOnlyDictionary<string, string>> RemoteEnvironment
        {
            get
            {
                if (_remoteEnv == null)
                    _remoteEnv = new AsyncLazy<IReadOnlyDictionary<string, string>>(async () =>
                    {
                        try
                        {
                            return await _channel.GetRemoteEnvironmentAsync().ConfigureAwait(false);
                        }
                        catch (ConnectionRefusedException)
                        {
                            return new Dictionary<string, string>();
                        }
                    }, VSPackage.TaskFactory);
                return _remoteEnv;
            }
        }
    }
}
