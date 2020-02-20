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

    public interface IProject
    {
        event ProjectLoaded Loaded;

        ProjectOptions Options { get; }
        string RootPath { get; }
        string GetRelativePath(string absoluteFilePath);
        string GetAbsolutePath(string projectRelativePath);
        Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint breakLine = 0, string[] watchesOverride = null);
        void SaveOptions();
    }

    [Export(typeof(IProject))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class Project : IProject
    {
        public event ProjectLoaded Loaded;

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
            Options.VisualizerColumnStyling.StylingChanged += SaveOptions;
            Loaded?.Invoke(Options);
        }

        public void SaveOptions() => Options.Write(_optionsFilePath);

        public string GetRelativePath(string absoluteFilePath)
        {
            if (!absoluteFilePath.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"\"{absoluteFilePath}\" does not belong to the current project located at \"{RootPath}\"");

            return absoluteFilePath.Substring(RootPath.Length + 1);
        }

        public string GetAbsolutePath(string projectRelativePath) =>
            Path.Combine(RootPath, projectRelativePath);

        #region MacroEvaluator
        private (IActiveCodeEditor, ICommunicationChannel, IProjectProperties)? _macroEvaluatorDependencies;

        public async Task<IMacroEvaluator> GetMacroEvaluatorAsync(uint breakLine = 0, string[] watchesOverride = null)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            if (!_macroEvaluatorDependencies.HasValue)
            {
                var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
                var activeCodeEditor = configuredProject.GetExport<IActiveCodeEditor>();
                var communicationChannel = configuredProject.GetExport<ICommunicationChannel>();

                var propertiesProvider = configuredProject.GetService<IProjectPropertiesProvider>("ProjectPropertiesProvider");
                var projectProperties = propertiesProvider.GetCommonProperties();

                _macroEvaluatorDependencies = (activeCodeEditor, communicationChannel, projectProperties);
            }
            var (codeEditor, channel, properties) = _macroEvaluatorDependencies.Value;

            var file = GetRelativePath(codeEditor.GetAbsoluteSourcePath());
            var line = codeEditor.GetCurrentLine();
            var transients = new MacroEvaluatorTransientValues(
                activeSourceFile: (file, line), breakLine: breakLine, watchesOverride: watchesOverride);

            var remoteEnvironment = new AsyncLazy<IReadOnlyDictionary<string, string>>(
                channel.GetRemoteEnvironmentAsync, VSPackage.TaskFactory);

            return new MacroEvaluator(properties, transients, remoteEnvironment, Options.DebuggerOptions, Options.Profile);
        }
        #endregion
    }
}
