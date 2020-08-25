using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    public sealed class SolutionProperties
    {
        public ProjectOptions Options { get; private set; }

        public void SetOptions(string configPath) =>
            Options = ProjectOptions.Read(configPath);
    }
}
