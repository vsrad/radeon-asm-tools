using System;
using System.Threading;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public interface IDocument : IDisposable
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; }
        ITextSnapshot CurrentSnapshot { get; }
        bool Disposed { get; }

        void OpenDocumentInEditor();
        void NavigateToPosition(int position);
        void ReplaceDocument(ITextDocument document);
    }

    public interface IReplaceableSnapshot
    {
        void OnDocumentChanged(ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot, CancellationToken cancellation);
    }
}
