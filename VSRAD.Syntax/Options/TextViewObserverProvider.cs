using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class TextViewObserverProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
        private readonly TextViewObserver _textViewObserver;

        [ImportingConstructor]
        public TextViewObserverProvider(IVsEditorAdaptersFactoryService adaptersFactoryService, TextViewObserver textViewObserver)
        {
            _adaptersFactoryService = adaptersFactoryService;
            _textViewObserver = textViewObserver;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var wpfTextView = _adaptersFactoryService.GetWpfTextView(textViewAdapter);
            _textViewObserver.WpfTextViewCreated(wpfTextView);
        }
    }
}
