using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public interface IDocumentFactory
    {
        IDocument GetOrCreateDocument(string path);
        IDocument GetOrCreateDocument(ITextBuffer buffer);
    }
}
