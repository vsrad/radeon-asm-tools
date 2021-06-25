using Microsoft.VisualStudio.Text;
using System;

namespace VSRAD.Syntax.Core
{
    public delegate void DocumentRenamedEventHandler(IDocument document, string oldPath, string newPath);
    public delegate void DocumentClosedEventHandler(IDocument document);

    public interface IDocument : IDisposable
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; }
        ITextSnapshot CurrentSnapshot { get; }

        event DocumentRenamedEventHandler DocumentRenamed;
        event DocumentClosedEventHandler DocumentClosed;
        void OpenDocumentInEditor();
        void NavigateToPosition(int position);
    }
}
