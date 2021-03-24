using System.Collections.Generic;
using VSRAD.Syntax.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Collapse
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    internal sealed class OutliningTaggerProvider : ITaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly Dictionary<IDocument, OutliningTagger> _outliningTaggers;

        [ImportingConstructor]
        public OutliningTaggerProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _outliningTaggers = new Dictionary<IDocument, OutliningTagger>();
            _documentFactory.DocumentDisposed += DocumentDisposed;
        }

        private void DocumentDisposed(IDocument document)
        {
            if (!_outliningTaggers.TryGetValue(document, out var tagger)) return;
            tagger.OnDestroy();
            _outliningTaggers.Remove(document);
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (!_outliningTaggers.TryGetValue(document, out var tagger))
            {
                tagger = new OutliningTagger(document.DocumentAnalysis);
                _outliningTaggers.Add(document, tagger);
            }

            return tagger as ITagger<T>;
        }
    }
}