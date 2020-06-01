using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.BuildTools;
using VSRAD.Package.Commands;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectLifecycle : IProjectDynamicLoadComponent
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly ExportProvider _exportProvider;

        [ImportingConstructor]
        public ProjectLifecycle(SVsServiceProvider serviceProvider, UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _unconfiguredProject = unconfiguredProject;
            _exportProvider = unconfiguredProject.Services.ExportProvider;
        }

        public async Task LoadAsync()
        {
            if (IsTemporaryVisualCProject(_unconfiguredProject))
                return;

            // Initialize our project components. We cannot use [Import] declarations because that would
            // result in components getting recreated for temporary VisualC projects
            // (particularly bad for DebuggerIntegration which has global state)
            var project = (Project)_exportProvider.GetExportedValue<IProject>();
            _unconfiguredProject.ProjectUnloading += async (s, e) =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                project.Unload();
                VSPackage.ProjectUnloaded();
            };

            _exportProvider.GetExportedValue<DebuggerIntegration>();
            _exportProvider.GetExportedValue<BreakpointIntegration>();
            _exportProvider.GetExportedValue<BuildToolsServer>();
            var toolWindowIntegration = _exportProvider.GetExportedValue<IToolWindowIntegration>();
            var commandRouter = _exportProvider.GetExportedValue<ICommandRouter>();

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            project.Load();
            await GetPackage().ProjectLoadedAsync(toolWindowIntegration, commandRouter);
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

        private static bool IsTemporaryVisualCProject(UnconfiguredProject unconfiguredProject)
        {
            // When opening an external source file, VisualC creates a temporary ("SingleFileISense") project.
            // We don't want to set up the plugin for those!

            // A better way of checking it would be reading the "Keyword" global property (= "SingleFileISense")
            // but UnconfiguredProject doesn't seem to allow that, and at the time LoadAsync runs
            // the ConfiguredProject hasn't been instantiated yet.
            return unconfiguredProject.FullPath.IndexOf(@"\SingleFileISense\", StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
