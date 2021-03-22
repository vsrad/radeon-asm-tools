using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public interface IDocument
    {
        IDocumentAnalysis DocumentAnalysis { get; }
        IDocumentTokenizer DocumentTokenizer { get; }
        string Path { get; set; }
        ITextSnapshot CurrentSnapshot { get; }
        bool IsDisposed { get; }

        void OpenDocumentInEditor();
        void NavigateToPosition(int position);
    }
}
