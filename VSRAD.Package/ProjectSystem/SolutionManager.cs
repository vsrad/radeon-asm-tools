using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using VSRAD.Package.Commands;

namespace VSRAD.Package.ProjectSystem
{
    public class ProjectLoadedEventArgs : EventArgs
    {
        public IToolWindowIntegration ToolWindowIntegration { get; set; }
        public ICommandRouter CommandRouter { get; set; }
    }

    public sealed class SolutionManager : IVsSelectionEvents
    {
        public event EventHandler<ProjectLoadedEventArgs> ProjectLoaded;

        private Project _currentRadProject;

        public SolutionManager(IVsMonitorSelection vsMonitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            vsMonitorSelection.AdviseSelectionEvents(this, out _);
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                if (varValueOld == varValueNew)
                    return VSConstants.S_OK;

                _currentRadProject?.Unload();
                _currentRadProject = null;

                if (varValueNew is IVsProject vsProject && GetCpsProject(vsProject) is UnconfiguredProject cpsProject)
                {
                    if (IsTemporaryVisualCProject(cpsProject))
                        return VSConstants.S_OK;

                    _currentRadProject = (Project)cpsProject.Services.ExportProvider.GetExportedValueOrDefault<IProject>();
                    if (_currentRadProject == null)
                        return VSConstants.S_OK;

                    _currentRadProject.Load();

                    var exportProvider = cpsProject.Services.ExportProvider;
                    var loadedEventArgs = new ProjectLoadedEventArgs
                    {
                        ToolWindowIntegration = exportProvider.GetExportedValue<IToolWindowIntegration>(),
                        CommandRouter = exportProvider.GetExportedValue<ICommandRouter>()
                    };
                    ProjectLoaded?.Invoke(this, loadedEventArgs);
                }
                else
                {
                    VSPackage.SolutionUnloaded();
                }
            }

            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        // https://github.com/microsoft/VSProjectSystem/blob/1c0a47aba5a22d3eb071dc097b73851bdeaf68db/doc/automation/finding_CPS_in_a_VS_project.md
        private static UnconfiguredProject GetCpsProject(IVsProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project is IVsBrowseObjectContext context && project is IVsHierarchy hierarchy)
            {
                if (ErrorHandler.Succeeded(hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out var extObject)))
                    if (extObject is EnvDTE.Project dteProject && dteProject.Object is IVsBrowseObjectContext dteContext)
                        context = dteContext;
                return context?.UnconfiguredProject;
            }
            return null;
        }

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
