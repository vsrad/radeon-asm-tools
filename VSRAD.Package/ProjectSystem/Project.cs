using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using VSRAD.Package.BuildTools;
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
        Task<IMacroEvaluator> GetMacroEvaluatorAsync();
        Task<IMacroEvaluator> GetMacroEvaluatorAsync(MacroEvaluatorTransientValues transients);
        MacroEvaluatorTransientValues GetMacroTransients();
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
        private readonly string _legacyOptionsFilePath;

        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public Project(UnconfiguredProject unconfiguredProject)
        {
            RootPath = Path.GetDirectoryName(unconfiguredProject.FullPath);
            _optionsFilePath = unconfiguredProject.FullPath + ".conf.json";
            _legacyOptionsFilePath = unconfiguredProject.FullPath + ".user.json";
            _unconfiguredProject = unconfiguredProject;
        }

        public void Load()
        {
            if (!File.Exists(_optionsFilePath) && File.Exists(_legacyOptionsFilePath))
                Options = ProjectOptions.ReadLegacy(_legacyOptionsFilePath);
            else
                Options = ProjectOptions.Read(_optionsFilePath);

            Options.PropertyChanged += OptionsPropertyChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;

            _unconfiguredProject.Services.ExportProvider.GetExportedValue<DebuggerIntegration>();
            _unconfiguredProject.Services.ExportProvider.GetExportedValue<BreakpointIntegration>();
            _unconfiguredProject.Services.ExportProvider.GetExportedValue<BuildToolsServer>();

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
        private ICommunicationChannel _communicationChannel;
        private IBreakpointTracker _breakpointTracker;

        public async Task<IMacroEvaluator> GetMacroEvaluatorAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            return await GetMacroEvaluatorAsync(GetMacroTransients());
        }

        public async Task<IMacroEvaluator> GetMacroEvaluatorAsync(MacroEvaluatorTransientValues transients)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (_communicationChannel == null)
                _communicationChannel = _unconfiguredProject.Services.ExportProvider.GetExportedValue<ICommunicationChannel>();

            var projectProperties = GetProjectProperties();
            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(
                _communicationChannel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);
            return new MacroEvaluator(projectProperties, transients, remoteEnvironment, Options.DebuggerOptions, Options.Profile);
        }

        public MacroEvaluatorTransientValues GetMacroTransients()
        {
            if (_codeEditor == null)
                _codeEditor = _unconfiguredProject.Services.ExportProvider.GetExportedValue<IActiveCodeEditor>();
            if (_breakpointTracker == null)
                _breakpointTracker = _unconfiguredProject.Services.ExportProvider.GetExportedValue<IBreakpointTracker>();

            var (file, breakLines) = _breakpointTracker.GetBreakTarget();
            var sourceLine = _codeEditor.GetCurrentLine();
            var watches = Options.DebuggerOptions.GetWatchSnapshot();
            return new MacroEvaluatorTransientValues(sourceLine, file, breakLines, watches);
        }
        #endregion
    }
}
