using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, RemotePlatform, _project.Options.DebuggerOptions, _dirtyProfile);

            var remotePlatform = await RemotePlatform.GetValueAsync();
            var evalTransients = new ActionEvaluationTransients("", "", _dirtyProfile.General.RunActionsLocally, remotePlatform, _dirtyProfile.Actions);

            return await step.EvaluateAsync(evaluator, evalTransients, sourceAction);
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
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, RemotePlatform, _project.Options.DebuggerOptions, _dirtyProfile);

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

        private AsyncLazy<IReadOnlyDictionary<string, string>> _remoteEnvironment;
        private AsyncLazy<IReadOnlyDictionary<string, string>> RemoteEnvironment
        {
            get
            {
                if (_dirtyProfile.General.RunActionsLocally)
                    return null;
                if (_remoteEnvironment == null)
                    _remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(async () =>
                    {
                        try
                        {
                            return await _channel.GetRemoteEnvironmentAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (ConnectionFailedException)
                        {
                            return new Dictionary<string, string>();
                        }
                    }, ThreadHelper.JoinableTaskFactory);
                return _remoteEnvironment;
            }
        }

        private AsyncLazy<OSPlatform> _remotePlatform;
        private AsyncLazy<OSPlatform> RemotePlatform
        {
            get
            {
                if (_remotePlatform == null)
                    _remotePlatform = new AsyncLazy<OSPlatform>(async () =>
                    {
                        if (_dirtyProfile.General.RunActionsLocally)
                            return OSPlatform.Windows;
                        try
                        {
                            return await _channel.GetRemotePlatformAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (ConnectionFailedException)
                        {
                            return OSPlatform.Windows;
                        }
                    }, ThreadHelper.JoinableTaskFactory);
                return _remotePlatform;
            }
        }

        private MacroEvaluatorTransientValues GetMacroTransients()
        {
            return new MacroEvaluatorTransientValues(0,
                sourcePath: "(ActiveEditorTabFullPath)",
                sourceDir: "(ActiveEditorTabDirectory)",
                sourceFile: "(ActiveEditorTabFileName)",
                debugPath: "(DebugStartupPath)",
                targetProcessor: "(TargetProcessor)");
        }
    }
}
