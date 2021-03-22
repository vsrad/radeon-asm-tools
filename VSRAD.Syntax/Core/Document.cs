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
    internal class Document : IDocument
    {
        public IDocumentAnalysis DocumentAnalysis { get; }
        public IDocumentTokenizer DocumentTokenizer { get; }
        public string Path { get; set; }
        public ITextSnapshot CurrentSnapshot => _textBuffer.CurrentSnapshot;
        public bool IsDisposed { get; private set; }

        protected readonly ILexer _lexer;
        protected readonly IParser _parser;
        private readonly ITextBuffer _textBuffer;

        public Document(ITextDocument textDocument, ILexer lexer, IParser parser)
        {
            _lexer = lexer;
            _parser = parser;
            _textBuffer = textDocument.TextBuffer;
            Path = textDocument.FilePath;
            DocumentTokenizer = new DocumentTokenizer(_textBuffer, _lexer);
            DocumentAnalysis = new DocumentAnalysis(this, DocumentTokenizer, _parser);
            IsDisposed = false;
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

            var vsTextBuffer = adapterService.GetBufferAdapter(_textBuffer);
            var hr = textManager.NavigateToPosition(vsTextBuffer, VSConstants.LOGVIEWID.TextView_guid, position, 0);
            if (hr != VSConstants.S_OK) throw Marshal.GetExceptionForHR(hr);
        }
    }
}
