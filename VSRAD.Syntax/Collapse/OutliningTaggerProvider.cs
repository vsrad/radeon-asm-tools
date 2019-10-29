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
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var parserManager = buffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());

            return buffer.Properties.GetOrCreateSingletonProperty(() => new OutliningTagger(buffer, parserManager) as ITagger<T>);
        }
    }
}