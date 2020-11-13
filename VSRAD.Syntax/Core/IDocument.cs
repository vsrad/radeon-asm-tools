using Microsoft.VisualStudio.Text;
using System;

namespace VSRAD.Syntax.Core
{
    public delegate void DocumentRenamedEventHandler(IDocument document, string oldPath, string newPath);

    public interface IDocument : IDisposable
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; }
        ITextSnapshot CurrentSnapshot { get; }
        bool IsDisposed { get; }

        event DocumentRenamedEventHandler DocumentRenamed;
        void OpenDocumentInEditor();
        void NavigateToPosition(int position);
    }
}
