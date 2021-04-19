using VSRAD.Syntax.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Collapse
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    internal sealed class OutliningTaggerProvider : DisposableProvider<IDocument, OutliningTagger>, ITaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public OutliningTaggerProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _documentFactory.DocumentDisposed += DisposeRequest;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (document == null) return null;

            return GetValue(document, () => 
                new OutliningTagger(document.DocumentAnalysis)) as ITagger<T>;
        }
    }
}