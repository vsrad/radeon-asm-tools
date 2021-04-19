using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
    public delegate void OnDestroyAction(IDocument sender);

    internal class Document : IDocument
    {
        public IDocumentAnalysis DocumentAnalysis { get; }
        public IDocumentTokenizer DocumentTokenizer => _tokenizer;
        public string Path => _textDocument.FilePath;
        public ITextSnapshot CurrentSnapshot => _textBuffer.CurrentSnapshot;
        public bool Disposed { get; private set; }

        protected readonly ILexer Lexer;
        protected readonly IParser Parser;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly OnDestroyAction _destroyAction;
        private readonly DocumentTokenizer _tokenizer;
        private ITextDocument _textDocument;
        private ITextBuffer _textBuffer;
        private CancellationTokenSource _cts;

        public Document(ITextDocumentFactoryService textDocumentFactory, ITextDocument textDocument, ILexer lexer, IParser parser, OnDestroyAction onDestroy)
        {
            _textDocumentFactory = textDocumentFactory;
            _textDocument = textDocument;
            _textBuffer = textDocument.TextBuffer;
            Lexer = lexer;
            Parser = parser;
            _destroyAction = onDestroy;
            _cts = new CancellationTokenSource();

            _tokenizer = new DocumentTokenizer(_textDocument.TextBuffer, Lexer, _cts.Token);
            DocumentAnalysis = new DocumentAnalysis(this, DocumentTokenizer, Parser);
            Disposed = false;

            _textDocumentFactory.TextDocumentDisposed += OnTextDocumentDisposed;
            _textBuffer.Changed += BufferChanged;
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

            if (Disposed || vsTextBuffer == null)
            {
                VsShellUtilities.OpenDocument(serviceProvider, Path, Guid.Empty, out _, out _, out var windowFrame);
                var textView = VsShellUtilities.GetTextView(windowFrame);

                if (textView.GetBuffer(out var vsTextLines) != VSConstants.S_OK) 
                    return;

                vsTextBuffer = vsTextLines;
            }

            var hr = textManager.NavigateToPosition(vsTextBuffer, VSConstants.LOGVIEWID.TextView_guid, position, 0);
            if (hr != VSConstants.S_OK) throw Marshal.GetExceptionForHR(hr);
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) =>
            _tokenizer.ApplyTextChanges(e, UpdateCancellation());

        protected void Cancel()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        protected CancellationToken UpdateCancellation()
        {
            Cancel();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        public void ReplaceDocument(ITextDocument document)
        {
            var cancellation = UpdateCancellation();
            var oldDocument = _textDocument;

            _textDocument = document;
            _textBuffer = document.TextBuffer;

            _tokenizer.OnDocumentChanged(oldDocument.TextBuffer.CurrentSnapshot, CurrentSnapshot, cancellation);
            oldDocument.Dispose();
        }

        public virtual void Dispose()
        {
            if (Disposed) return;

            Cancel();
            _textBuffer.Changed -= BufferChanged;
            _textDocumentFactory.TextDocumentDisposed -= OnTextDocumentDisposed;

            DocumentTokenizer.OnDestroy();
            DocumentAnalysis.OnDestroy();

            Disposed = true;
            _destroyAction.Invoke(this);
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (e.TextDocument == _textDocument)
                Dispose();
        }
    }
}
