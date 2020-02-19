using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.BuildTools;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.PreprocessCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class PreprocessCommand : BaseBuildWithPreviewCommand
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public PreprocessCommand(IProject project, BuildToolsServer buildServer, SVsServiceProvider serviceProvider)
            : base(BuildSteps.Preprocessor, buildServer, Constants.PreprocessCommandId, serviceProvider)
        {
            _project = project;
        }

        protected override async Task<(string localPath, string lineMarker)> ConfigurePreviewAsync()
        {
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await Options.PreprocessorProfileOptions.EvaluateAsync(evaluator);
            return (options.LocalOutputCopyPath, options.LineMarker);
        }
    }
}
