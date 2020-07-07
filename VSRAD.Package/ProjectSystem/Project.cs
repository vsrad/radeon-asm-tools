﻿using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void ProjectLoaded(ProjectOptions options);
    public delegate void ProjectUnloaded();

    public interface IProject
    {
        event ProjectLoaded Loaded;
        event ProjectUnloaded Unloaded;

        ProjectOptions Options { get; }
        string RootPath { get; } // TODO: Replace all usages with IProjectSourceManager.ProjectRoot
        IProjectProperties GetProjectProperties();
        Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint[] breakLines = null, string[] watchesOverride = null);
        void SaveOptions();
    }

    [Export(typeof(IProject))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class Project : IProject
    {
        public event ProjectLoaded Loaded;
        public event ProjectUnloaded Unloaded;

        public ProjectOptions Options { get; private set; }

        public string RootPath { get; }

        private readonly string _optionsFilePath;
        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public Project(UnconfiguredProject unconfiguredProject)
        {
            RootPath = Path.GetDirectoryName(unconfiguredProject.FullPath);
            _optionsFilePath = unconfiguredProject.FullPath + ".user.json";
            _unconfiguredProject = unconfiguredProject;
        }

        public void Load()
        {
            Options = ProjectOptions.Read(_optionsFilePath);
            Options.PropertyChanged += OptionsPropertyChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;
            Loaded?.Invoke(Options);
        }

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e) => SaveOptions();

        public void Unload() => Unloaded?.Invoke();

        public void SaveOptions() => Options.Write(_optionsFilePath);

        public IProjectProperties GetProjectProperties()
        {
            var configuredProject = _unconfiguredProject.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
            return configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
        }

        #region MacroEvaluator
        private IActiveCodeEditor _codeEditor;
        private IProjectSourceManager _projectSourceManager;
        private ICommunicationChannel _communicationChannel;

        public async Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint[] breakLines = null, string[] watchesOverride = null)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (_codeEditor == null)
                _codeEditor = _unconfiguredProject.Services.ExportProvider.GetExportedValue<IActiveCodeEditor>();
            if (_projectSourceManager == null)
                _projectSourceManager = _unconfiguredProject.Services.ExportProvider.GetExportedValue<IProjectSourceManager>();
            if (_communicationChannel == null)
                _communicationChannel = _unconfiguredProject.Services.ExportProvider.GetExportedValue<ICommunicationChannel>();

            var projectProperties = GetProjectProperties();

            var file = await GetRelativeSourcePathAsync();
            var line = _codeEditor.GetCurrentLine();
            var transients = new MacroEvaluatorTransientValues(activeSourceFile: (file, line), breakLines, watchesOverride);

            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(
                _communicationChannel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);

            return new MacroEvaluator(projectProperties, transients, remoteEnvironment, Options.DebuggerOptions, Options.Profile);
        }

        private async Task<string> GetRelativeSourcePathAsync()
        {
            var sourcePath = _codeEditor.GetAbsoluteSourcePath();
            if (sourcePath.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
                return sourcePath.Substring(RootPath.Length + 1);

            foreach (var (absolutePath, relativePath) in await _projectSourceManager.ListProjectFilesAsync())
                if (absolutePath == sourcePath)
                    return relativePath;

            throw new ArgumentException($"\"{sourcePath}\" does not belong to the current project located at \"{RootPath}\"");
        }
        #endregion
    }
}
