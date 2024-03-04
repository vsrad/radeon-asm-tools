using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class ToolWindowCommand : ICommandHandler
    {
        public Guid CommandSet => Constants.ToolWindowCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            switch (commandId)
            {
                case Constants.ToolWindowVisualizerCommandId:
                case Constants.ToolWindowOptionsCommandId:
                case Constants.ToolWindowSliceVisualizerCommandId:
                case Constants.ToolWindowFloatInspectorCommandId:
                    return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
                default:
                    return 0;
            }
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            switch (commandId)
            {
                case Constants.ToolWindowVisualizerCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.VisualizerToolWindow.Frame).Show());
                    break;
                case Constants.ToolWindowOptionsCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.OptionsToolWindow.Frame).Show());
                    break;
                case Constants.ToolWindowSliceVisualizerCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.SliceVisualizerToolWindow.Frame).Show());
                    break;
                case Constants.ToolWindowFloatInspectorCommandId:
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)VSPackage.FloatInspectorToolWindow.Frame).Show());
                    break;
            }
        }
    }
}