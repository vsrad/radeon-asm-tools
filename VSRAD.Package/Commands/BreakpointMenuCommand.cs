using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.Utils;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointMenuCommand : ICommandHandler
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IBreakpointTracker _breakpointTracker;

        [ImportingConstructor]
        public BreakpointMenuCommand(SVsServiceProvider serviceProvider, IActiveCodeEditor codeEditor, IBreakpointTracker breakpointTracker)
        {
            _serviceProvider = serviceProvider;
            _codeEditor = codeEditor;
            _breakpointTracker = breakpointTracker;

        }

        public Guid CommandSet => Constants.BreakpointMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            var flags = OleCommandText.GetFlags(commandText);

            if (commandId == Constants.BreakpointMenuToggleResumable)
            {
                var line = _codeEditor.GetCurrentLine();
                var file = _codeEditor.GetAbsoluteSourcePath();
                var resumable = _breakpointTracker.GetResumableState(file, line) ? OLECMDF.OLECMDF_LATCHED : 0;
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | resumable;
            }

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var file = _codeEditor.GetAbsoluteSourcePath();
            var line = _codeEditor.GetCurrentLine();
            if (commandId == Constants.BreakpointMenuToggleResumable)
            {
                var resumable = _breakpointTracker.GetResumableState(file, line);
                _breakpointTracker.SetResumableState(file, line, !resumable);
            }
            else if (commandId == Constants.BreakpointMenuAllToResumable || commandId == Constants.BreakpointMenuAllToUnresumable)
            {
                var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);

                bool state = commandId == Constants.BreakpointMenuAllToResumable ? true : false;
                foreach (Breakpoint br in dte.Debugger.Breakpoints)
                {
                    if (br.File == file)
                    {
                        _breakpointTracker.SetResumableState(br.File, (uint)(br.FileLine - 1), state);
                    }
                }

            }
        }
    }
}
