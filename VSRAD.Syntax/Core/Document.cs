using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Runtime.InteropServices;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
    public delegate void OnDestroyAction(IDocument sender);

    internal class Document : IDocument
    {
        public IDocumentAnalysis DocumentAnalysis { get; }
        public IDocumentTokenizer DocumentTokenizer { get; }
        public string Path => _textDocument.FilePath;
        public ITextSnapshot CurrentSnapshot => TextBuffer.CurrentSnapshot;
        public bool IsDisposed { get; private set; }

        protected readonly ILexer Lexer;
        protected readonly IParser Parser;
        private readonly ITextDocument _textDocument;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly OnDestroyAction _destroyAction;
        private ITextBuffer TextBuffer => _textDocument.TextBuffer;

        public Document(ITextDocumentFactoryService textDocumentFactory, ITextDocument textDocument, ILexer lexer, IParser parser, OnDestroyAction onDestroy)
        {
            _textDocumentFactory = textDocumentFactory;
            _textDocument = textDocument;
            Lexer = lexer;
            Parser = parser;
            _destroyAction = onDestroy;
            DocumentTokenizer = new DocumentTokenizer(TextBuffer, Lexer);
            DocumentAnalysis = new DocumentAnalysis(this, DocumentTokenizer, Parser);
            IsDisposed = false;

            _textDocumentFactory.TextDocumentDisposed += OnTextDocumentDisposed;
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (e.TextDocument == _textDocument)
                Dispose();
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

            if (IsDisposed) OpenDocumentInEditor();

            var vsTextBuffer = adapterService.GetBufferAdapter(TextBuffer);
            var hr = textManager.NavigateToPosition(vsTextBuffer, VSConstants.LOGVIEWID.TextView_guid, position, 0);
            if (hr != VSConstants.S_OK) throw Marshal.GetExceptionForHR(hr);
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            _textDocumentFactory.TextDocumentDisposed -= OnTextDocumentDisposed;
            DocumentTokenizer.OnDestroy();
            DocumentAnalysis.OnDestroy();
            _destroyAction.Invoke(this);
        }
    }
}
