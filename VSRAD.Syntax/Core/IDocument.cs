using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public delegate void DocumentRenamedEventHandler(IDocument document, string oldPath, string newPath);

    public interface IDocument
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; }
        ITextSnapshot CurrentSnapshot { get; }

        event DocumentRenamedEventHandler DocumentRenamed;
    }
}
