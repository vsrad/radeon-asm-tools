using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;
using VSRAD.Package.BuildTools;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    public abstract class BaseBuildWithPreviewCommand : BaseRemoteCommand
    {
        private readonly BuildToolsServer _buildServer;
        private readonly DTE2 _dte;
        private readonly BuildSteps _buildSteps;

        private BuildEvents _buildEvents;
        private (string localPath, string lineMarker)? _ongoingRun;

        protected BaseBuildWithPreviewCommand(
            Guid commandSet,
            int commandId,
            BuildSteps buildSteps,
            BuildToolsServer buildServer,
            SVsServiceProvider serviceProvider) : base(commandSet, commandId, serviceProvider)
        {
            _buildSteps = buildSteps;
            _buildServer = buildServer;
            _dte = serviceProvider.GetService(typeof(DTE)) as DTE2;
            Assumes.Present(_dte);
        }

        protected abstract Task<(string localPath, string lineMarker)> ConfigurePreviewAsync();

        public override async Task RunAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            if (_buildEvents == null)
            {
                _buildEvents = _dte.Events.BuildEvents;
                _buildEvents.OnBuildProjConfigDone += OnBuildFinished;
            }

            _ongoingRun = await ConfigurePreviewAsync();
            _buildServer.OverrideStepsForNextBuild(_buildSteps);
            _dte.ExecuteCommand("Build.BuildSolution");
        }

        private void OnBuildFinished(string project, string projectConfig, string platform, string solutionConfig, bool success)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_ongoingRun != null && success)
                OpenFileInEditor(_ongoingRun.Value.localPath, _ongoingRun.Value.lineMarker);
            _ongoingRun = null;
        }
    }
}
