using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Package.ProjectSystem
{
    public class SolutionManager : IVsSelectionEvents
    {
        public SolutionManager(IVsMonitorSelection vsMonitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            uint cookie;
            vsMonitorSelection.AdviseSelectionEvents(this, out cookie);
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                // TODO: handle change of startup project here
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
