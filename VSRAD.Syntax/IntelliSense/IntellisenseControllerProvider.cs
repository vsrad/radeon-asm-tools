using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace VSRAD.Syntax.IntelliSense
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class IntellisenseControllerProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
        private readonly INavigationTokenService _navigationService;
        private readonly IPeekBroker _peekBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;

        [ImportingConstructor]
        public IntellisenseControllerProvider(RadeonServiceProvider editorService, IPeekBroker peekBroker, ISignatureHelpBroker signatureHelpBroker, INavigationTokenService navigationService)
        {
            _adaptersFactoryService = editorService.EditorAdaptersFactoryService;
            _peekBroker = peekBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _navigationService = navigationService;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _adaptersFactoryService.GetWpfTextView(textViewAdapter);
            if (view == null) return;

            var filter = new IntellisenseController(_peekBroker, _signatureHelpBroker, _navigationService, view);
            textViewAdapter.AddCommandFilter(filter, out var next);
            filter.Next = next;
        }
    }
}
