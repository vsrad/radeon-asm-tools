using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class AddToWatchesCommand : ICommandHandler
    {
        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;

        [ImportingConstructor]
        public AddToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
        }

        public Guid CommandSet => Constants.AddToWatchesCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.MenuCommandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            return 0;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId != Constants.MenuCommandId)
                return;

            var activeWord = _codeEditor.GetActiveWord();
            if (!string.IsNullOrWhiteSpace(activeWord))
            {
                var watchName = activeWord.Trim();
                _toolIntegration.AddWatchFromEditor(watchName);
            }
        }
    }
}
