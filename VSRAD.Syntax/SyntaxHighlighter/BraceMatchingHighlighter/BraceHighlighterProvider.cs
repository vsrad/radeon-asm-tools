using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter.BraceMatchingHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class BraceHighlighterProvider : IViewTaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly Dictionary<IDocument, BraceHighlighter> _taggers;

        [ImportingConstructor]
        public BraceHighlighterProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _documentFactory.DocumentDisposed += OnDocumentDestroy;
            _taggers = new Dictionary<IDocument, BraceHighlighter>();
        }

        private void OnDocumentDestroy(IDocument document)
        {
            if (!_taggers.TryGetValue(document, out var tagger)) return;
            tagger.OnDestroy();
            _taggers.Remove(document);
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var document = _documentFactory.GetOrCreateDocument(buffer);
            var tagger = new BraceHighlighter(textView, buffer, document.DocumentTokenizer);
            _taggers.Add(document, tagger);

            return tagger as ITagger<T>;
        }
    }

}
