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
    internal sealed class OutliningTaggerAsmProvider : ITaggerProvider
    {
        [Import]
        private readonly IDocumentFactory _documentFactory;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);

            return buffer.Properties.GetOrCreateSingletonProperty(() => new OutliningTagger(document.DocumentAnalysis) as ITagger<T>);
        }
    }
}