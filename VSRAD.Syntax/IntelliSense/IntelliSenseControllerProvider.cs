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
    internal class IntelliSenseControllerProvider : IVsTextViewCreationListener
    {
        private readonly RadeonServiceProvider _editorService;
        private readonly INavigationTokenService _navigationService;

        [ImportingConstructor]
        public IntelliSenseControllerProvider(RadeonServiceProvider editorService, INavigationTokenService navigationService)
        {
            _editorService = editorService;
            _navigationService = navigationService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _editorService.EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            if (view == null) return;

            var filter = view.Properties.GetOrCreateSingletonProperty(
                () => new IntelliSenseController(_editorService, _navigationService, view));

            textViewAdapter.AddCommandFilter(filter, out var next);
            filter.Next = next;
        }
    }
}
