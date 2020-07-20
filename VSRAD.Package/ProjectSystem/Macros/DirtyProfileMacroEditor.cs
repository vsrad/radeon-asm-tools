﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
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

        public async Task EditObjectPropertyAsync(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            var oldValue = (string)property.GetValue(target);
            var newValue = await EditAsync(propertyName, oldValue);
            property.SetValue(target, newValue);
        }

        public async Task<string> EditAsync(string macroName, string currentValue)
        {
            var projectProperties = _project.GetProjectProperties();
            var transients = new MacroEvaluatorTransientValues(sourceLine: 0,
                sourcePath: "<current source full path>",
                sourceDir: "<current source dir name>",
                sourceFile: "<current source file name>");

            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(async () =>
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

            var evaluator = new MacroEvaluator(projectProperties, transients, remoteEnvironment, _project.Options.DebuggerOptions, _dirtyProfile);

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var editor = new MacroEditContext(macroName, currentValue, evaluator);
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() =>
                editor.LoadPreviewListAsync(_dirtyProfile.Macros, projectProperties, remoteEnvironment));

            var editorWindow = new MacroEditorWindow(editor)
            {
                Owner = Application.Current.MainWindow,
                ShowInTaskbar = false
            };
            editorWindow.ShowDialog();

            return editor.MacroValue;
        }
    }
}
