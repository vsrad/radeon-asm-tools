using Microsoft.VisualStudio.Text;
using System;

namespace VSRAD.Syntax.Core
{
    internal class Document : IDocument
    {
        public Document(ITextDocument textDocument)
        {
            Path = textDocument.FilePath;
            _textBuffer = textDocument.TextBuffer;

            textDocument.FileActionOccurred += FileActionOccurred;
        }

        public IDocumentAnalysis DocumentAnalysis => throw new NotImplementedException();
        public IDocumentTokenizer DocumentTokenizer => throw new NotImplementedException();
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
