using EnvDTE;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    public sealed class SolutionProperties
    {
        public ProjectOptions Options { get; private set; }
        private string _optionsFilePath;

        public void SetOptions(Solution solution, UnconfiguredProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solutionPath = solution.FullName;
            var isSolutionExists = !string.IsNullOrWhiteSpace(solutionPath);
            var legacyOptionsPath = project.FullPath + ".conf.json";
            var legacyOptionsPathAternative = project.FullPath + ".user.json";
            var solutionConfigPath = isSolutionExists ? GetConfigPath(solutionPath) : "";

            Options = isSolutionExists && File.Exists(solutionConfigPath)
                ? ProjectOptions.Read(solutionConfigPath)
                : File.Exists(legacyOptionsPath)
                    ? ProjectOptions.Read(legacyOptionsPath)
                    : File.Exists(legacyOptionsPathAternative)
                        ? ProjectOptions.ReadLegacy(legacyOptionsPathAternative)
                        : new ProjectOptions();

            _optionsFilePath = !isSolutionExists
                // we opened project outside of any solution
                // we'll save profiles to the legacy options location
                ? legacyOptionsPath
                // we opened solution with at least one project
                // we'll save profiles to the new location in solution dir
                : solutionConfigPath;

            Options.PropertyChanged += OptionsPropertyChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;
        }

        public static string GetConfigPath(string solutionPath)
        {
            var lastIndex = solutionPath.LastIndexOf(".sln", System.StringComparison.OrdinalIgnoreCase);
            if (lastIndex == -1)
                return solutionPath + ".conf.json";
            else return solutionPath.Remove(lastIndex, 4) + ".conf.json";
        }

        public void ResetOptions() => Options = null;

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e) => Options.Write(_optionsFilePath);
    }
}
