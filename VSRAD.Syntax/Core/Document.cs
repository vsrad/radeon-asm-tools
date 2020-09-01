using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;

namespace VSRAD.Syntax.Core
{
    internal class Document : IDocument
    {
        public Document(ITextDocument textDocument, ILexer lexer, IParser parser)
        {
            _textBuffer = textDocument.TextBuffer;
            Path = textDocument.FilePath;
            DocumentTokenizer = new DocumentTokenizer(_textBuffer, lexer);
            DocumentAnalysis = new DocumentAnalysis(DocumentTokenizer, parser);

            textDocument.FileActionOccurred += FileActionOccurred;
        }

        public IDocumentAnalysis DocumentAnalysis { get; private set; }
        public IDocumentTokenizer DocumentTokenizer { get; private set; }
        public string Path { get; private set; }
        public ITextSnapshot CurrentSnapshot => _textBuffer.CurrentSnapshot;

        public event DocumentRenamedEventHandler DocumentRenamed;

        private readonly ITextBuffer _textBuffer;

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType == FileActionTypes.DocumentRenamed)
            {
                var oldPath = Path;
                Path = e.FilePath;
                DocumentRenamed?.Invoke(this, oldPath, Path);
            }
        }
    }
}
