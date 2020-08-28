using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Core;
using EnvDTE;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class IdentifierHighliterTaggerProvider : IViewTaggerProvider
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public IdentifierHighliterTaggerProvider(INavigationTokenService navigationTokenService, IDocumentFactory documentFactory)
        {
            _navigationTokenService = navigationTokenService;
            _documentFactory = documentFactory;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var document = _documentFactory.GetOrCreateDocument(buffer);
            return new HighlightWordTagger(textView, buffer, documentAnalysis, _navigationTokenService) as ITagger<T>;
        }
    }
}
