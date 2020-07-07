using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser
{
    internal class DocumentInfo
    {
        private readonly ITextDocument _textDocument;

        public string Path => _textDocument.FilePath;
        public string DirectoryPath => System.IO.Path.GetDirectoryName(Path);
        public ITextSnapshot CurrentSnapshot => _textDocument.TextBuffer.CurrentSnapshot;

        public DocumentInfo(ITextDocument textDocument)
        {
            _textDocument = textDocument;
        }
    }
}
