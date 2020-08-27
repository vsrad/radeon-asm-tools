using EnvDTE;
using Microsoft;
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
            var legacyOptionsPath = project.FullPath + ".conf.json";
            var legacyOptionsPathAternative = project.FullPath + ".user.json";

            // we opened project outside of any solution
            // load legacy path if exist, load legacy alternative if exists
            // if both not exist than create default options
            // we'll save profiles to the legacy options location
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                Options = File.Exists(legacyOptionsPath)
                    ? ProjectOptions.Read(legacyOptionsPath)
                    : File.Exists(legacyOptionsPathAternative)
                        ? ProjectOptions.ReadLegacy(legacyOptionsPathAternative)
                        : new ProjectOptions();
                _optionsFilePath = legacyOptionsPath;
            }
            // we opened solution with at least one project
            // trying to retrieve any version of obsolete profiles
            // we'll save profiles to the new location in solution dir
            else
            {
                Options = File.Exists(legacyOptionsPath)
                    ? ProjectOptions.Read(legacyOptionsPath)
                    : File.Exists(legacyOptionsPathAternative)
                        ? ProjectOptions.ReadLegacy(legacyOptionsPathAternative)
                        : new ProjectOptions();
                _optionsFilePath = GetConfigPath(solutionPath);
            }

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
