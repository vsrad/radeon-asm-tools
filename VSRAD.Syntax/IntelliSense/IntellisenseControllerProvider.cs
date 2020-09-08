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
        private readonly INavigationService _navigationService;

        [ImportingConstructor]
        public IntellisenseControllerProvider(RadeonServiceProvider editorService, INavigationService navigationService)
        {
            _editorService = editorService;
            _navigationService = navigationService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _editorService.EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            if (view == null) return;

            var filter = view.Properties.GetOrCreateSingletonProperty(
                () => new IntellisenseController(_editorService, _navigationService, view));

            textViewAdapter.AddCommandFilter(filter, out var next);
            filter.Next = next;
        }
    }
}
