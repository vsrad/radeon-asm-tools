using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class DirtyProfileMacroEditor
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly ProfileOptions _dirtyProfile;

        public DirtyProfileMacroEditor(
            IProject project,
            ICommunicationChannel channel,
            ProfileOptions dirtyProfile)
        {
            _project = project;
            _channel = channel;
            _dirtyProfile = dirtyProfile;
        }

        public async Task<Result<IActionStep>> EvaluateStepAsync(IActionStep step, string sourceAction)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var transients = GetMacroTransients();
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            return await step.EvaluateAsync(evaluator, _dirtyProfile, sourceAction);
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var transients = GetMacroTransients();
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            var editor = new MacroEditContext(macroName, currentValue, evaluator);
            ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(() =>
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
                if (_dirtyProfile.General.RunActionsLocally)
                    return null;
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
                    }, ThreadHelper.JoinableTaskFactory);
                return _remoteEnv;
            }
        }

        private MacroEvaluatorTransientValues GetMacroTransients()
        {
            return new MacroEvaluatorTransientValues(0,
                sourcePath: "<active editor tab full path>",
                debugPath: "<debug startup path>",
                targetProcessor: "<target processor>",
                sourceDir: "<active editor tab dir name>",
                sourceFile: "<active editor tab file name>");
        }
    }
}
