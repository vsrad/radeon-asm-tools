using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class AddToWatchesCommand : ICommandHandler
    {
        private static readonly Regex EmptyBracketsRegex = new Regex(@"\[\s*\]", RegexOptions.Compiled);

        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public AddToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor, SVsServiceProvider serviceProvider)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
            _serviceProvider = serviceProvider;
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

            ThreadHelper.ThrowIfNotOnUIThread();

            var activeWord = _codeEditor.GetActiveWord();

            if (!string.IsNullOrWhiteSpace(activeWord))
            {
                var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);
                var selectionText = (dte.ActiveDocument.Selection as TextSelection).Text;
                // dont omit empty brackets if user manually selected text with them
                var watchName = string.IsNullOrWhiteSpace(selectionText)
                    ? EmptyBracketsRegex.Replace(activeWord, "").Trim()
                    : selectionText.Trim();
                _toolIntegration.AddWatchFromEditor(watchName);
            }
        }
    }
}
