using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
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
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var transients = GetMacroTransients();
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            var actionEvalEnv = new ActionEvaluationEnvironment("", "", _dirtyProfile.General.RunActionsLocally,
                new DebugServer.IPC.CapabilityInfo(default, default, default), _dirtyProfile.Actions);

            return await step.EvaluateAsync(evaluator, actionEvalEnv, sourceAction);
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
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var transients = GetMacroTransients();
            var evaluator = new MacroEvaluator(ProjectProperties, transients, RemoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

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
                    }, VSPackage.TaskFactory);
                return _remoteEnv;
            }
        }

        private IActiveCodeEditor _codeEditor;
        private IBreakpointTracker _breakpointTracker;

        private MacroEvaluatorTransientValues GetMacroTransients()
        {
            if (_codeEditor == null)
                _codeEditor = _project.UnconfiguredProject.Services.ExportProvider.GetExportedValue<IActiveCodeEditor>();
            if (_breakpointTracker == null)
                _breakpointTracker = _project.UnconfiguredProject.Services.ExportProvider.GetExportedValue<IBreakpointTracker>();
            try
            {
                var (file, breakLines) = _breakpointTracker.GetBreakTarget();
                var sourceLine = _codeEditor.GetCurrentLine();
                var watches = _project.Options.DebuggerOptions.GetWatchSnapshot();
                return new MacroEvaluatorTransientValues(sourceLine, file, breakLines, watches);
            }
            catch (UserException e) when (e.Message == ActiveCodeEditor.NoFilesOpenError)
            {
                return new MacroEvaluatorTransientValues(0,
                    sourcePath: "<current source full path>",
                    new[] { 0u },
                    _project.Options.DebuggerOptions.GetWatchSnapshot(),
                    sourceDir: "<current source dir name>",
                    sourceFile: "<current source file name>");
            }
        }
    }
}
