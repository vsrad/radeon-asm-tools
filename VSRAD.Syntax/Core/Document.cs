using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
    internal class Document : IDocument
    {
        public IDocumentAnalysis DocumentAnalysis { get; }
        public IDocumentTokenizer DocumentTokenizer { get; }
        public string Path { get; private set; }
        public ITextSnapshot CurrentSnapshot => _textBuffer.CurrentSnapshot;
        public bool IsDisposed { get; private set; }

        public event DocumentRenamedEventHandler DocumentRenamed;
        public event DocumentClosedEventHandler DocumentClosed;

        private readonly ITextDocument _textDocument;
        private readonly ITextBuffer _textBuffer;

        public Document(ITextDocument textDocument, ILexer lexer, IParser parser)
        {
            _textDocument = textDocument;
            _textBuffer = _textDocument.TextBuffer;
            Path = _textDocument.FilePath;
            DocumentTokenizer = new DocumentTokenizer(_textBuffer, lexer);
            DocumentAnalysis = new DocumentAnalysis(this, DocumentTokenizer, parser);
            IsDisposed = false;

            _textDocument.FileActionOccurred += FileActionOccurred;
        }

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType.HasFlag(FileActionTypes.DocumentRenamed))
            {
                var oldPath = Path;
                Path = e.FilePath;
                DocumentRenamed?.Invoke(this, oldPath, Path);
            }
        }

        public virtual void OpenDocumentInEditor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = ServiceProvider.GlobalProvider;
            VsShellUtilities.OpenDocument(serviceProvider, Path);
        }

        public void NavigateToPosition(int position)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceProvider = ServiceProvider.GlobalProvider;

            var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            var adapterService = serviceProvider.GetMefService<IVsEditorAdaptersFactoryService>();
            Assumes.Present(textManager);
            Assumes.Present(adapterService);

            var vsTextBuffer = adapterService.GetBufferAdapter(_textBuffer);
            Assumes.NotNull(vsTextBuffer);

            ErrorHandler.ThrowOnFailure(textManager.NavigateToPosition(vsTextBuffer, 
                VSConstants.LOGVIEWID.TextView_guid, 
                iPos: position, 
                iLen: 0));
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;

            _textDocument.FileActionOccurred -= FileActionOccurred;

            _textDocument.Dispose();
            DocumentAnalysis.Dispose();
            DocumentTokenizer.Dispose();

            IsDisposed = true;
            DocumentClosed?.Invoke(this);
        }
    }
}
