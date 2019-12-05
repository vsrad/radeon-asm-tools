using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.PreprocessCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class PreprocessCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly BuildTools.BuildToolsServer _buildServer;

        [ImportingConstructor]
        public PreprocessCommand(
            IProject project,
            BuildTools.BuildToolsServer buildServer,
            SVsServiceProvider serviceProvider) : base(Constants.PreprocessCommandId, serviceProvider)
        {
            _project = project;
            _buildServer = buildServer;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Preprocessor.EvaluateAsync(evaluator);

            _buildServer.OverrideStepsForNextBuild(BuildTools.BuildSteps.Preprocessor);

            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            Assumes.Present(dte);
            dte.ExecuteCommand("Build.BuildSolution");
            dte.Events.BuildEvents.OnBuildDone += (vsBuildScope scope, vsBuildAction action) =>
                OpenFileInEditor(options.LocalOutputCopyPath, options.LineMarker);
        }
    }
}
