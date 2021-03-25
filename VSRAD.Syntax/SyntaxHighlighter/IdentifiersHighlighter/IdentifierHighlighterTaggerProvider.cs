using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class IdentifierHighlighterTaggerProvider : DisposableProvider<IDocument, HighlightWordTagger>, IViewTaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public IdentifierHighlighterTaggerProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _documentFactory.DocumentDisposed += DisposeRequest;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer) return null;

            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (document == null) return null;

            return GetValue(document, () => new HighlightWordTagger(textView, buffer, document.DocumentAnalysis)) as ITagger<T>;
        }
    }
}
