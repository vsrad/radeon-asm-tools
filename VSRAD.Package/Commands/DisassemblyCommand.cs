using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.Package.ProjectSystem;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.DisassemblyCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class DisassemblyCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly BuildTools.BuildToolsServer _buildServer;

        [ImportingConstructor]
        public DisassemblyCommand(
            IProject project,
            BuildTools.BuildToolsServer buildServer,
            SVsServiceProvider serviceProvider) : base(Constants.DisassembleCommandId, serviceProvider)
        {
            _project = project;
            _buildServer = buildServer;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Disassembler.EvaluateAsync(evaluator);

            _buildServer.OverrideStepsForNextBuild(BuildTools.BuildSteps.Disassembler);

            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            Assumes.Present(dte);
            dte.ExecuteCommand("Build.BuildSolution");
            dte.Events.BuildEvents.OnBuildProjConfigDone += (string project, string projectConfig, string platform, string solutionConfig, bool success) =>
                OpenFileInEditor(options.LocalOutputCopyPath, options.LineMarker);
        }
    }
}
