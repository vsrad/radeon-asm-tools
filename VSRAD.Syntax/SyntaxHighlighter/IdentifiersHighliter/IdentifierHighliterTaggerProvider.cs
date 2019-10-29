using VSRAD.Syntax.Peek.DefinitionService;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(HighlightWordTag))]
    internal sealed class IdentifierHighliterTaggerProvider : IViewTaggerProvider
    {
        private readonly ITextSearchService2 TextSearchService;
        private readonly ITextStructureNavigatorSelectorService TextStructureNavigatorSelector;
        private readonly DefinitionService DefinitionService;

        [ImportingConstructor]
        public IdentifierHighliterTaggerProvider(ITextSearchService2 textSearchService,
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService,
            DefinitionService definitionService)
        {
            this.TextSearchService = textSearchService;
            this.TextStructureNavigatorSelector = textStructureNavigatorSelectorService;
            this.DefinitionService = definitionService;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var textStructureNavigator = TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

            return new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator, DefinitionService) as ITagger<T>;
        }
    }
}
