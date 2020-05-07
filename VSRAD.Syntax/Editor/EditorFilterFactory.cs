using VSRAD.Syntax.Peek.DefinitionService;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense.Completion;

namespace VSRAD.Syntax.Editor
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class EditorFilterFactory : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
        private readonly DefinitionService _definitionService;

        [ImportingConstructor]
        public EditorFilterFactory(IVsEditorAdaptersFactoryService adaptersFactoryService, 
            DefinitionService definitionService)
        {
            this._adaptersFactoryService = adaptersFactoryService;
            this._definitionService = definitionService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (view != null)
            {
                var filter = new EditorFilter(_definitionService, view);

                textViewAdapter.AddCommandFilter(filter, out var next);
                filter.Next = next;
            }
        }
    }
}
