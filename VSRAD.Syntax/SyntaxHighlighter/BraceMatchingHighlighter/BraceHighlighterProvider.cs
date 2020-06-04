using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.SyntaxHighlighter.BraceMatchingHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class BraceHighlighterProvider : IViewTaggerProvider
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        [ImportingConstructor]
        public BraceHighlighterProvider(DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(buffer);
            return new BraceHighlighter(textView, buffer, documentAnalysis) as ITagger<T>;
        }
    }

}
