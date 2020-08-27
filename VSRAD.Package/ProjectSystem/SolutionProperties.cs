using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.ComponentModel.Composition;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    public sealed class SolutionProperties
    {
        public ProjectOptions Options { get; private set; }
        private string _optionsFilePath;

        public void SetOptions(string configPath)
        {
            _optionsFilePath = configPath;
            Options = ProjectOptions.Read(configPath);
            Options.PropertyChanged += OptionsPropertyChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerOptions.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerAppearance.PropertyChanged += OptionsPropertyChanged;
            Options.VisualizerColumnStyling.PropertyChanged += OptionsPropertyChanged;
        }

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e) => Options.Write(_optionsFilePath);
    }
}
