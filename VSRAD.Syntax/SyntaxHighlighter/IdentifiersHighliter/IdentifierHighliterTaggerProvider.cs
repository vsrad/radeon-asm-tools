using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class IdentifierHighliterTaggerProvider : IViewTaggerProvider
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        [ImportingConstructor]
        public IdentifierHighliterTaggerProvider(INavigationTokenService navigationTokenService, DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _navigationTokenService = navigationTokenService;
            _documentAnalysisProvoder = documentAnalysisProvoder;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(buffer);
            return new HighlightWordTagger(textView, buffer, documentAnalysis, _navigationTokenService) as ITagger<T>;
        }
    }
}
