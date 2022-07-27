using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(IErrorTag))]
    internal sealed class SyntaxErrorHighlighterTaggerProvider : IViewTaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public SyntaxErrorHighlighterTaggerProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (document == null) return null;

            return buffer.Properties.GetOrCreateSingletonProperty(() =>  
                new SyntaxErrorHighlighterTagger(document)) as ITagger<T>;
        }
    }
}
