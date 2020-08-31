using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace VSRAD.Package.ProjectSystem
{
    public class SolutionManager : IVsSelectionEvents
    {
        private readonly Solution _solution;

        public SolutionManager(IVsMonitorSelection vsMonitorSelection, Solution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            uint cookie;
            vsMonitorSelection.AdviseSelectionEvents(this, out cookie);

            _solution = solution;
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                var startupProjectPath = ((Array)_solution.SolutionBuild.StartupProjects).GetValue(0) as string;
                var project = _solution.Item(startupProjectPath);
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
    }
}
