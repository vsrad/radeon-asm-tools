using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
    internal class Document : IDocument, IDisposable
    {
        public IDocumentAnalysis DocumentAnalysis { get; private set; }
        public IDocumentTokenizer DocumentTokenizer { get; private set; }
        public string Path { get; private set; }
        public ITextSnapshot CurrentSnapshot => _textBuffer.CurrentSnapshot;
        public bool IsDisposed { get; private set; }

        public event DocumentRenamedEventHandler DocumentRenamed;

        protected ITextDocument _textDocument;
        protected ILexer _lexer;
        protected IParser _parser;
        private ITextBuffer _textBuffer;

        public void Initialize(ITextDocument textDocument, ILexer lexer, IParser parser)
        {
            _lexer = lexer;
            _parser = parser;
            _textDocument = textDocument;
            _textBuffer = _textDocument.TextBuffer;
            Path = _textDocument.FilePath;
            DocumentTokenizer = new DocumentTokenizer(_textBuffer, _lexer);
            DocumentAnalysis = new DocumentAnalysis(this, DocumentTokenizer, _parser);
            IsDisposed = false;

            _textDocument.FileActionOccurred += FileActionOccurred;
        }

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType == FileActionTypes.DocumentRenamed)
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

        public virtual void NavigateToPosition(int position)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = ServiceProvider.GlobalProvider;
            var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            var adapterService = serviceProvider.GetMefService<IVsEditorAdaptersFactoryService>();

            var vsTextBuffer = adapterService.GetBufferAdapter(_textBuffer);
            var hr = textManager.NavigateToPosition(vsTextBuffer, VSConstants.LOGVIEWID.TextView_guid, position, 0);
            if (hr != VSConstants.S_OK) Error.LogError(Marshal.GetExceptionForHR(hr));
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                _textDocument.FileActionOccurred -= FileActionOccurred;
                _textDocument.Dispose();
                IsDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~Document()
        {
            Dispose();
        }
    }
}
