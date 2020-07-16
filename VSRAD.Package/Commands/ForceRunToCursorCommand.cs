using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class ForceRunToCursorCommand : ICommandHandler
    {
        private readonly ProjectSystem.DebuggerIntegration _debugger;

        [ImportingConstructor]
        public ForceRunToCursorCommand(ProjectSystem.DebuggerIntegration debugger)
        {
            _debugger = debugger;
        }

        public Guid CommandSet => Constants.ForceRunToCursorCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.MenuCommandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            return 0;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (commandId == Constants.MenuCommandId)
                _debugger.RunToCurrentLine();
        }
    }
}