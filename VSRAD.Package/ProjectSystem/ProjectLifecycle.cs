using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.BuildTools;
using VSRAD.Package.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectLifecycle : IProjectDynamicLoadComponent
    {
        private readonly IProject _project;

        private readonly SVsServiceProvider _serviceProvider;
        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public ProjectLifecycle(IProject project, SVsServiceProvider serviceProvider, UnconfiguredProject unconfiguredProject)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            _unconfiguredProject = unconfiguredProject;
            _unconfiguredProject.ProjectUnloading += ProjectUnloadingAsync;
        }

        [Import]
        private EditorExtensions.QuickInfoEvaluateSelectedState QuickInfoState { get; set; }
        [Import]
        private DebuggerIntegration Debugger { get; set; }
        [Import]
        private BreakpointIntegration Breakpoints { get; set; }
        [Import]
        private BuildToolsServer BuildServer { get; set; }

        [Export(typeof(IToolWindowIntegration))]
        private IToolWindowIntegration ToolWindowIntegration { get; set; }

        public async Task LoadAsync()
        {
            //await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            //((Project)_project).Load();
            //QuickInfoState.SetProjectOnLoad(_project);
            //Debugger.SetProjectOnLoad(_project);

            //var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            //ToolWindowIntegration = new ToolWindowIntegration(configuredProject, _project, Debugger);

            //await GetPackage().ProjectLoadedAsync(ToolWindowIntegration);
        }

        private async Task ProjectUnloadingAsync(object sender, EventArgs args)
        {
            //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //((Project)_project).Unload();
            //VSPackage.ProjectUnloaded();
        }

        private VSPackage GetPackage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var shell = _serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
            Assumes.Present(shell);

            if (shell.IsPackageLoaded(Constants.PackageGuid, out var package) != VSConstants.S_OK)
                ErrorHandler.ThrowOnFailure(shell.LoadPackage(Constants.PackageGuid, out package));

            return (VSPackage)package;
        }

        public Task UnloadAsync() => Task.CompletedTask; /* https://github.com/microsoft/VSProjectSystem/issues/287 */
    }
}
