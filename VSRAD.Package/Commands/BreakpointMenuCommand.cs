using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointMenuCommand : ICommandHandler
    {
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public BreakpointMenuCommand(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Guid CommandSet => Constants.BreakpointMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            var flags = OleCommandText.GetFlags(commandText);

            if (commandId == Constants.BreakpointMenuToggleResumable)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_LATCHED;

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.BreakpointMenuToggleResumable)
            {

            }
        }
    }
}
