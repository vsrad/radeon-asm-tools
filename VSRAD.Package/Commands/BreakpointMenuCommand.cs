using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointMenuCommand : ICommandHandler
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public BreakpointMenuCommand(IProject project)
        {
            _project = project;
        }

        public Guid CommandSet => Constants.BreakpointMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.ToggleMultipleBreakpointsCommandId)
            {
                var enabled = _project.Options.DebuggerOptions.EnableMultipleBreakpoints;

                var flags = OleCommandText.GetFlags(commandText);
                if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                    OleCommandText.SetText(commandText, "Multiple breakpoints mode is " + (enabled ? "enabled" : "disabled"));

                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | (enabled ? OLECMDF.OLECMDF_LATCHED : 0);
            }
            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.ToggleMultipleBreakpointsCommandId)
            {
                _project.Options.DebuggerOptions.EnableMultipleBreakpoints = !_project.Options.DebuggerOptions.EnableMultipleBreakpoints;
            }
        }
    }
}
