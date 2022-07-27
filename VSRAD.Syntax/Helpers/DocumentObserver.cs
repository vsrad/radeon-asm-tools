using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.Helpers
{
    public abstract class DocumentObserver
    {
        protected DocumentObserver(IDocument document)
        {
            document.DocumentClosed += DocumentClosedEventHandler;
        }

        protected abstract void OnClosingDocument(IDocument document);

        private void DocumentClosedEventHandler(IDocument document)
        {
            document.DocumentClosed -= DocumentClosedEventHandler;
            OnClosingDocument(document);
        }
    }
}
