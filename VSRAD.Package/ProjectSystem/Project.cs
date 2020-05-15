using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void ProjectLoaded(ProjectOptions options);
    public delegate void ProjectUnloaded();

    public interface IProject
    {
        event ProjectLoaded Loaded;
        event ProjectUnloaded Unloaded;

        bool ProfileInitialized { get; }
        ProjectOptions Options { get; }
        string RootPath { get; } // TODO: Replace all usages with IProjectSourceManager.ProjectRoot
        Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint[] breakLines = null, string[] watchesOverride = null);
        void SaveOptions();
    }

    [Export(typeof(IProject))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class Project : IProject
    {
        public event ProjectLoaded Loaded;
        public event ProjectUnloaded Unloaded;

        public bool ProfileInitialized => Options.Profile != null;
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
            Options.PropertyChanged += (s, e) => SaveOptions();
            Options.DebuggerOptions.PropertyChanged += (s, e) => SaveOptions();
            Options.VisualizerOptions.PropertyChanged += (s, e) => SaveOptions();
            Options.VisualizerAppearance.PropertyChanged += (s, e) => SaveOptions();
            Options.VisualizerColumnStyling.PropertyChanged += (s, e) => SaveOptions();
            Loaded?.Invoke(Options);
        }

        public void Unload()
        {
           //SaveOptions();
            Unloaded?.Invoke();
        }

        public void SaveOptions() => Options.Write(_optionsFilePath);

        #region MacroEvaluator
        private (IActiveCodeEditor, IProjectSourceManager, ICommunicationChannel, IProjectProperties)? _macroEvaluatorDependencies;

        public async Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint[] breakLines = null, string[] watchesOverride = null)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (!_macroEvaluatorDependencies.HasValue)
            {
                var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
                var activeCodeEditor = configuredProject.GetExport<IActiveCodeEditor>();
                var projectSourceManager = configuredProject.GetExport<IProjectSourceManager>();
                var communicationChannel = configuredProject.GetExport<ICommunicationChannel>();

                var propertiesProvider = configuredProject.GetService<IProjectPropertiesProvider>("ProjectPropertiesProvider");
                var projectProperties = propertiesProvider.GetCommonProperties();

                _macroEvaluatorDependencies = (activeCodeEditor, projectSourceManager, communicationChannel, projectProperties);
            }
            var (codeEditor, sourceManager, channel, properties) = _macroEvaluatorDependencies.Value;

            var file = await GetRelativeSourcePathAsync(codeEditor, sourceManager);
            var line = codeEditor.GetCurrentLine();
            var transients = new MacroEvaluatorTransientValues(activeSourceFile: (file, line), breakLines, watchesOverride);

            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(
                channel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);

            return new MacroEvaluator(properties, transients, remoteEnvironment, Options.DebuggerOptions, Options.Profile);
        }

        private async Task<string> GetRelativeSourcePathAsync(IActiveCodeEditor codeEditor, IProjectSourceManager sourceManager)
        {
            var sourcePath = codeEditor.GetAbsoluteSourcePath();
            if (sourcePath.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
                return sourcePath.Substring(RootPath.Length + 1);

            foreach (var (absolutePath, relativePath) in await sourceManager.ListProjectFilesAsync())
                if (absolutePath == sourcePath)
                    return relativePath;

            throw new ArgumentException($"\"{sourcePath}\" does not belong to the current project located at \"{RootPath}\"");
        }
        #endregion
    }
}
