using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.BuildTools;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.DisassemblyCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class DisassemblyCommand : BaseBuildWithPreviewCommand
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public DisassemblyCommand(IProject project, BuildToolsServer buildServer, SVsServiceProvider serviceProvider)
            : base(BuildSteps.Disassembler, buildServer, Constants.PreprocessCommandId, serviceProvider)
        {
            _project = project;
        }

        protected override async Task<(string localPath, string lineMarker)> ConfigurePreviewAsync()
        {
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Disassembler.EvaluateAsync(evaluator);
            return (options.LocalOutputCopyPath, options.LineMarker);
        }
    }
}
