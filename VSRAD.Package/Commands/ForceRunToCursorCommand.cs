using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class ForceRunToCursorCommand : ICommandHandler
    {
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly IDebuggerIntegration _debuggerIntegration;

        [ImportingConstructor]
        public ForceRunToCursorCommand(IProjectSourceManager projectSourceManager, IBreakpointTracker breakpointTracker, IDebuggerIntegration debuggerIntegration)
        {
            _projectSourceManager = projectSourceManager;
            _breakpointTracker = breakpointTracker;
            _debuggerIntegration = debuggerIntegration;
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
            {
                var activeEditor = _projectSourceManager.GetActiveEditorView();
                var (currentFile, currentLine) = (activeEditor.GetFilePath(), activeEditor.GetCaretPos().Line);
                var debugFile = _projectSourceManager.DebugStartupPath ?? currentFile;
                _breakpointTracker.SetRunToLine(debugFile, currentFile, currentLine);
                _debuggerIntegration.Execute(step: false);
            }
        }
    }
}