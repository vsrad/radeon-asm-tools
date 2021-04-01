using System;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public interface IDocument : IDisposable
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; }
        ITextSnapshot CurrentSnapshot { get; }
        bool IsDisposed { get; }

        void OpenDocumentInEditor();
        void NavigateToPosition(int position);
        void ReplaceDocument(ITextDocument document);
    }

    public interface IReplaceableSnapshot
    {
        void OnDocumentChanged(ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot);
    }
}
