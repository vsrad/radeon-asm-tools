using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
    internal class InvisibleDocument : Document
    {
        private readonly IDocumentFactory _documentFactory;
        private IDocument visibleDocument;

        public InvisibleDocument(ITextDocumentFactoryService textDocumentFactory, IDocumentFactory documentFactory, ITextDocument textDocument, ILexer lexer, IParser parser, OnDestroyAction onDestroy)
            : base(textDocumentFactory, textDocument, lexer, parser, onDestroy)
        {
            _documentFactory = documentFactory;
        }

        public override void NavigateToPosition(int position)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (visibleDocument != null)
            {
                visibleDocument.NavigateToPosition(position);
                return;
            }

            var serviceProvider = ServiceProvider.GlobalProvider;
            var adapterService = serviceProvider.GetMefService<IVsEditorAdaptersFactoryService>();

            VsShellUtilities.OpenDocument(serviceProvider, Path, Guid.Empty, out _, out _, out var windowFrame);
            var textView = VsShellUtilities.GetTextView(windowFrame);
            if (textView.GetBuffer(out var vsTextBuffer) == VSConstants.S_OK)
            {
                var textBuffer = adapterService.GetDocumentBuffer(vsTextBuffer);
                var document = _documentFactory.GetOrCreateDocument(textBuffer);
                if (document != null)
                {
                    visibleDocument = document;
                    document.NavigateToPosition(position);
                    return;
                }
            }

            throw new InvalidOperationException($"Cannot open document {Path}");
        }

        public IDocument ToVisibleDocument(Func<ILexer, IParser, IDocument> factory) =>
            factory.Invoke(Lexer, Parser);
    }
}
