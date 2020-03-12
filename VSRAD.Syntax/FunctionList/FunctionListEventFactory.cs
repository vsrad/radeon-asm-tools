using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.FunctionList
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class FunctionListEventFactory : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;

        [ImportingConstructor]
        public FunctionListEventFactory(IVsEditorAdaptersFactoryService adaptersFactoryService)
        {
            this._adaptersFactoryService = adaptersFactoryService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (view != null)
                view.Caret.PositionChanged += (obj, args) => ThreadHelper.JoinableTaskFactory.RunAsync(() => FunctionList.TryHighlightCurrentFunctionAsync(args));
        }
    }
}
