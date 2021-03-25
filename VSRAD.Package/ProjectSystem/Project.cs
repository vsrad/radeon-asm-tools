using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Package.BuildTools;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void ProjectLoaded(ProjectOptions options);
    public delegate void ProjectUnloaded();

    public interface IProject
    {
        event ProjectUnloaded Unloaded;

        ProjectOptions Options { get; }
        UnconfiguredProject UnconfiguredProject { get; }
        string RootPath { get; } // TODO: Replace all usages with IProjectSourceManager.ProjectRoot

        void RunWhenLoaded(Action<ProjectOptions> callback);
        IProjectProperties GetProjectProperties();
        TExport GetExportByMetadataAndType<TExport, TMetadata>(Predicate<TMetadata> metadataFilter, Predicate<TExport> exportFilter) where TExport : class;
        void SaveOptions();
    }

    [Export(typeof(IProject))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class Project : IProject
    {
        public event ProjectUnloaded Unloaded;

        public ProjectOptions Options { get; private set; }
        public UnconfiguredProject UnconfiguredProject { get; }
        public string RootPath { get; }

        private readonly string _visualOptionsFilePath;
        private readonly string _optionsFilePath;

        private bool _loaded = false;
        private readonly List<Action<ProjectOptions>> _onLoadCallbacks = new List<Action<ProjectOptions>>();

        [ImportingConstructor]
        public Project(UnconfiguredProject unconfiguredProject)
        {
            RootPath = Path.GetDirectoryName(unconfiguredProject.FullPath);
            _optionsFilePath = unconfiguredProject.FullPath + ".conf.json";
            _visualOptionsFilePath = unconfiguredProject.FullPath + ".viz.json";
            UnconfiguredProject = unconfiguredProject;
        }

        public void RunWhenLoaded(Action<ProjectOptions> callback)
        {
            if (_loaded)
                callback(Options);
            else
                _onLoadCallbacks.Add(callback);
        }

        public void Load(ProjectOptions projectOptions)
        {
            if (projectOptions == null)
            {
                Options = ProjectOptions.Read(_visualOptionsFilePath, _optionsFilePath);

                Options.PropertyChanged += OptionsPropertyChanged;
                Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
                Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
                Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
                Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;
            }
            else
            {
                Options = projectOptions;
            }

            UnconfiguredProject.Services.ExportProvider.GetExportedValue<BreakpointIntegration>();
            UnconfiguredProject.Services.ExportProvider.GetExportedValue<BuildToolsServer>();

            _loaded = true;
            foreach (var callback in _onLoadCallbacks)
                callback(Options);
            _onLoadCallbacks.Clear();
        }

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e) => SaveOptions();

        public void Unload() => Unloaded?.Invoke();

        public void SaveOptions() => Options.Write(_visualOptionsFilePath, _optionsFilePath);

        public IProjectProperties GetProjectProperties()
        {
            var configuredProject = UnconfiguredProject.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
            return configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
        }

        public TExport GetExportByMetadataAndType<TExport, TMetadata>(Predicate<TMetadata> metadataFilter, Predicate<TExport> exportFilter) where TExport : class
        {
            return UnconfiguredProject.Services.ExportProvider.GetExports<TExport, TMetadata>()
                .Where(e => metadataFilter(e.Metadata))
                .FirstOrDefault(e => exportFilter(e.Value))?.Value;
        }
    }
}
