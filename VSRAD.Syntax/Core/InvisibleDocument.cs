using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    internal class InvisibleDocument : Document
    {
        private IDocument _visibleDocument;

        public override void NavigateToPosition(int position)
        {
            if (_visibleDocument == null || _visibleDocument.IsDisposed)
                base.OpenDocumentInEditor();

            _visibleDocument.NavigateToPosition(position);
        }

        public IDocument ToVisibleDocument(ITextDocument textDocument)
        {
            var document = new Document();
            document.Initialize(textDocument, _lexer, _parser);
            Dispose();

            _visibleDocument = document;
            return _visibleDocument;
        }
    }
}
