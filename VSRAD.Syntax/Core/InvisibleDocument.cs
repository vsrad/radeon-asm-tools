using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;

namespace VSRAD.Syntax.Core
{
    internal class InvisibleDocument : Document
    {
        private IDocument _visibleDocument;

        public InvisibleDocument(ITextDocument textDocument, ILexer lexer, IParser parser)
            : base(textDocument, lexer, parser) { }

        public override void NavigateToPosition(int position)
        {
            if (_visibleDocument == null || _visibleDocument.IsDisposed)
                OpenDocumentInEditor();

            _visibleDocument.NavigateToPosition(position);
        }

        public IDocument ToVisibleDocument(ITextDocument textDocument)
        {
            Dispose();

            _visibleDocument = new Document(textDocument, _lexer, _parser);
            return _visibleDocument;
        }
    }
}
