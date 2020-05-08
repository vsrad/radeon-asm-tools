using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(HighlightWordTag))]
    internal sealed class IdentifierHighliterTaggerProvider : IViewTaggerProvider
    {
        private readonly ITextSearchService2 TextSearchService;
        private readonly NavigationTokenService DefinitionService;

        [ImportingConstructor]
        public IdentifierHighliterTaggerProvider(ITextSearchService2 textSearchService,
            NavigationTokenService definitionService)
        {
            this.TextSearchService = textSearchService;
            this.DefinitionService = definitionService;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            return new HighlightWordTagger(textView, buffer, TextSearchService, DefinitionService) as ITagger<T>;
        }
    }
}
