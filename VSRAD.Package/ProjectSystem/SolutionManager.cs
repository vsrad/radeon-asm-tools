﻿using EnvDTE;
using EnvDTE80;
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

        private bool TryGetDteProject(Solution sln, string project, out EnvDTE.Project dteProject)
        {
            try
            {
                dteProject = sln.Item(project);
                return true;
            }
            catch (ArgumentException)
            {
                dteProject = null;
                return false;
            }
        }

        // VS can load our extension after opening a solution (and raising OnElementValueChanged),
        // so we need to check the startup project manually after the extension is loaded
        public void LoadCurrentSolution(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte.Solution is Solution sln && sln.SolutionBuild.StartupProjects is Array sp && sp.GetValue(0) is string startupProject)
            {
                if (!TryGetDteProject(sln, startupProject, out var dteProject)) return;
                if (GetCpsProject(dteProject) is UnconfiguredProject cpsProject)
                    LoadRadProject(cpsProject);
            }
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
                    LoadRadProject(cpsProject);
                else
                    VSPackage.SolutionUnloaded();
            }

            return VSConstants.S_OK;
        }

        private void LoadRadProject(UnconfiguredProject cpsProject)
        {
            if (IsTemporaryVisualCProject(cpsProject))
                return;

            var cpsExportProvider = cpsProject.Services.ExportProvider;
            if (cpsExportProvider.GetExportedValueOrDefault<IProject>() is Project project && project.TryLoad())
            {
                _currentRadProject = project;
                var loadedEventArgs = new ProjectLoadedEventArgs
                {
                    ToolWindowIntegration = cpsExportProvider.GetExportedValue<IToolWindowIntegration>(),
                    CommandRouter = cpsExportProvider.GetExportedValue<ICommandRouter>()
                };
                ProjectLoaded?.Invoke(this, loadedEventArgs);
            }
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
        private static UnconfiguredProject GetCpsProject(IVsProject vsProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (vsProject is IVsBrowseObjectContext ctx) // RADProject
                return ctx.UnconfiguredProject;

            if (vsProject is IVsHierarchy hierarchy) // VisualC
                if (ErrorHandler.Succeeded(hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out var extObject)))
                    if (extObject is EnvDTE.Project dteProject && dteProject.Object is IVsBrowseObjectContext dteCtx)
                        return dteCtx.UnconfiguredProject;

            return null;
        }

        private static UnconfiguredProject GetCpsProject(EnvDTE.Project dteProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dteProject is IVsBrowseObjectContext ctx) // RADProject
                return ctx.UnconfiguredProject;

            if (dteProject.Object is IVsBrowseObjectContext objCtx) // VisualC
                return objCtx.UnconfiguredProject;

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
