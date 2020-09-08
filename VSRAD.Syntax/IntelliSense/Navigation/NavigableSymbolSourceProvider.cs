using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    [Export(typeof(INavigableSymbolSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(NavigableSymbolSourceProvider))]
    internal sealed class NavigableSymbolSourceProvider : INavigableSymbolSourceProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly INavigationTokenService _navigationService;

        [ImportingConstructor]
        public NavigableSymbolSourceProvider(IDocumentFactory documentFactory, INavigationTokenService navigationService)
        {
            _documentFactory = documentFactory;
            _navigationService = navigationService;
        }

        public INavigableSymbolSource TryCreateNavigableSymbolSource(ITextView textView, ITextBuffer buffer)
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            return (document != null)
                ? new NavigableSymbolSource(document.DocumentAnalysis, _navigationService)
                : null;
        }
    }
}
