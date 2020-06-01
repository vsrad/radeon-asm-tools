using VSRAD.Syntax.Parser;
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
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(buffer);

            return buffer.Properties.GetOrCreateSingletonProperty(() => new OutliningTagger(documentAnalysis) as ITagger<T>);
        }
    }
}