using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.Package.BuildTools;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void ProjectLoaded(ProjectOptions options);
    public delegate void ProjectUnloaded();

    public interface IProject
    {
        event ProjectLoaded Loaded;
        event ProjectUnloaded Unloaded;

        ProjectOptions Options { get; }
        UnconfiguredProject UnconfiguredProject { get; }
        string RootPath { get; } // TODO: Replace all usages with IProjectSourceManager.ProjectRoot
        IProjectProperties GetProjectProperties();
        void SaveOptions();
    }

    [Export(typeof(IProject))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class Project : IProject
    {
        public event ProjectLoaded Loaded;
        public event ProjectUnloaded Unloaded;

        public ProjectOptions Options { get; private set; }
        public UnconfiguredProject UnconfiguredProject { get; }
        public string RootPath { get; }

        private readonly string _optionsFilePath;
        private readonly string _legacyOptionsFilePath;

        [ImportingConstructor]
        public Project(UnconfiguredProject unconfiguredProject)
        {
            RootPath = Path.GetDirectoryName(unconfiguredProject.FullPath);
            _optionsFilePath = unconfiguredProject.FullPath + ".conf.json";
            _legacyOptionsFilePath = unconfiguredProject.FullPath + ".user.json";
            UnconfiguredProject = unconfiguredProject;
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

            UnconfiguredProject.Services.ExportProvider.GetExportedValue<BreakpointIntegration>();
            UnconfiguredProject.Services.ExportProvider.GetExportedValue<BuildToolsServer>();

            Loaded?.Invoke(Options);
        }

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e) => SaveOptions();

        public void Unload() => Unloaded?.Invoke();

        public void SaveOptions() => Options.Write(_optionsFilePath);

        public IProjectProperties GetProjectProperties()
        {
            var configuredProject = UnconfiguredProject.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
            return configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
        }
    }
}
