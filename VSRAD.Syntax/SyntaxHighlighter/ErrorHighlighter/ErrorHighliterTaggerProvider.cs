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
    [TagType(typeof(IErrorTag))]
    internal sealed class ErrorHighliterTaggerProvider : IViewTaggerProvider
    {
        [ImportingConstructor]
        public ErrorHighliterTaggerProvider()
        {
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            return new ErrorHighlighterTagger(textView, buffer) as ITagger<T>;
        }
    }
}
