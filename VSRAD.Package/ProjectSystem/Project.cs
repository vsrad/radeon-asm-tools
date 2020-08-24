using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
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

        private string _optionsFilePath;
        private readonly SVsServiceProvider _serviceProvider;

        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public Project(UnconfiguredProject unconfiguredProject, SVsServiceProvider serviceProvider)
        {
            RootPath = Path.GetDirectoryName(unconfiguredProject.FullPath);
            _unconfiguredProject = unconfiguredProject;
            _serviceProvider = serviceProvider;
        }

        public void Load()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            var solutionPath = dte.Solution.FullName;
            _optionsFilePath = string.IsNullOrWhiteSpace(solutionPath)
                ? _unconfiguredProject.FullPath + ".conf.json"
                : GetConfigPath(dte.Solution.FullName);

            var legacyOptionsFilePath = _unconfiguredProject.FullPath + ".conf.json";
            var legacyOptionsFilePathAlternative = _unconfiguredProject.FullPath + ".user.json";

            if (File.Exists(_optionsFilePath))
                Options = ProjectOptions.Read(_optionsFilePath);
            else if (File.Exists(legacyOptionsFilePath))
                Options = ProjectOptions.Read(legacyOptionsFilePath);
            else
                Options = ProjectOptions.ReadLegacy(legacyOptionsFilePathAlternative);

            SaveOptions(); // create options file if we read legacy

            Options.PropertyChanged += OptionsPropertyChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;
            Loaded?.Invoke(Options);
        }

        private string GetConfigPath(string solutionPath)
        {
            var lastIndex = solutionPath.LastIndexOf(".sln");
            if (lastIndex == -1)
                return solutionPath + ".conf.json";
            else return solutionPath.Remove(lastIndex, 4) + ".conf.json";
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

            var sourceLine = _codeEditor.GetCurrentLine();
            var sourcePath = _codeEditor.GetAbsoluteSourcePath();
            var transients = new MacroEvaluatorTransientValues(sourceLine, sourcePath, breakLines: breakLines, watchesOverride: watchesOverride);

            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(
                _communicationChannel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);

            return new MacroEvaluator(projectProperties, transients, remoteEnvironment, Options.DebuggerOptions, Options.Profile);
        }
        #endregion
    }
}
