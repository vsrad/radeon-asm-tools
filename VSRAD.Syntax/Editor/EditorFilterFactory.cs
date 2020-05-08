using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Editor
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class EditorFilterFactory : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;

        [ImportingConstructor]
        public EditorFilterFactory(RadeonServiceProvider editorService)
        {
            _adaptersFactoryService = editorService.EditorAdaptersFactoryService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (view != null)
            {
                var filter = new EditorFilter(view);

                textViewAdapter.AddCommandFilter(filter, out var next);
                filter.Next = next;
            }
        }
    }
}
