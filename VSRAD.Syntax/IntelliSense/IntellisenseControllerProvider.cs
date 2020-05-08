using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class IntellisenseControllerProvider : IVsTextViewCreationListener
    {
        private readonly RadeonServiceProvider _editorService;
        private readonly NavigationTokenService _navigationTokenService;

        [ImportingConstructor]
        public IntellisenseControllerProvider(RadeonServiceProvider editorService, NavigationTokenService navigationTokenService)
        {
            _editorService = editorService;
            _navigationTokenService = navigationTokenService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _editorService.EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (view != null)
            {
                var filter = view.Properties.GetOrCreateSingletonProperty(() => new IntellisenseController(_editorService, _navigationTokenService, view));

                textViewAdapter.AddCommandFilter(filter, out var next);
                filter.Next = next;
            }
        }
    }
}
