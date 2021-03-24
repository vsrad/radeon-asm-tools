using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class IdentifierHighlighterTaggerProvider : IViewTaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly Dictionary<IDocument, HighlightWordTagger> _taggers;

        [ImportingConstructor]
        public IdentifierHighlighterTaggerProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _documentFactory.DocumentDisposed += OnDocumentDestroy;
            _taggers = new Dictionary<IDocument, HighlightWordTagger>();
        }

        private void OnDocumentDestroy(IDocument document)
        {
            if (!_taggers.TryGetValue(document, out var tagger)) return;

            _taggers.Remove(document);
            tagger.OnDestroy();
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var document = _documentFactory.GetOrCreateDocument(buffer);
            var tagger = new HighlightWordTagger(textView, buffer, document.DocumentAnalysis);
            _taggers.Add(document, tagger);

            return tagger as ITagger<T>;
        }
    }
}
