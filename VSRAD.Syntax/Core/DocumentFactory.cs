using EnvDTE;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    [Export(typeof(IDocumentFactory))]
    internal partial class DocumentFactory : IDocumentFactory
    {
        private readonly ContentTypeManager _contentTypeManager;
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly Dictionary<string, IDocument> _documents;
        private readonly Lazy<IInstructionListManager> _instructionManager;


        public event ActiveDocumentChangedEventHandler ActiveDocumentChanged;
        public event DocumentCreatedEventHandler DocumentCreated;
        public event DocumentDisposedEventHandler DocumentDisposed;

        [ImportingConstructor]
        public DocumentFactory(RadeonServiceProvider serviceProvider,
            ContentTypeManager contentTypeManager,
            Lazy<IInstructionListManager> instructionManager)
        {
            _instructionManager = instructionManager;

            _documents = new Dictionary<string, IDocument>();
            _contentTypeManager = contentTypeManager;
            _serviceProvider = serviceProvider;
            _serviceProvider.TextDocumentFactoryService.TextDocumentDisposed += TextDocumentDisposed;

            var dte = _serviceProvider.ServiceProvider.GetService(typeof(DTE)) as DTE;
            dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
        }

        public IDocument GetOrCreateDocument(string path)
        {
            if (_documents.TryGetValue(path, out var document))
                return document;

            var contentType = _contentTypeManager.DetermineContentType(path);
            if (contentType == null)
                return null;

            var textDocument = _serviceProvider
                .TextDocumentFactoryService
                .CreateAndLoadTextDocument(path, contentType);

            return CreateDocument(textDocument, (lexer, parser) => new InvisibleDocument(this, textDocument, lexer, parser));
        }

        public IDocument GetOrCreateDocument(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(typeof(IDocument), out IDocument document))
                return document;

            var textDocument = buffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            if (_documents.TryGetValue(textDocument.FilePath, out document)
                && document is InvisibleDocument invisibleDocument)
            {
                document = invisibleDocument.ToVisibleDocument(textDocument);
                ObserveDocument(document, textDocument);
            }
            else
            {
                switch (buffer.GetAsmType())
                {
                    case AsmType.RadAsm:
                    case AsmType.RadAsm2:
                        document = CreateDocument(textDocument, (lexer, parser) => new CodeDocument(_instructionManager.Value, textDocument, lexer, parser)); 
                        break;
                    case AsmType.RadAsmDoc:
                        document = CreateDocument(textDocument, (lexer, parser) => new Document(textDocument, lexer, parser)); 
                        break;
                }
            }

            // CreateDocument can return null if document does not belong to RadAsmSyntax
            if (document != null) DocumentCreated?.Invoke(document);
            return document;
        }

        private IDocument CreateDocument(ITextDocument textDocument, Func<ILexer, IParser, IDocument> creator)
        {
            var lexerParser = GetLexerParser(textDocument.TextBuffer.GetAsmType());
            if (!lexerParser.HasValue) return null;

            var document = creator(lexerParser.Value.Lexer, lexerParser.Value.Parser);
            ObserveDocument(document, textDocument);

            return document;
        }

        private void ObserveDocument(IDocument document, ITextDocument textDocument)
        {
            document.DocumentRenamed += DocumentRenamed;
            textDocument.TextBuffer.Properties.AddProperty(typeof(IDocument), document);

            _documents.Add(document.Path, document);
        }

        private void TextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (_documents.TryGetValue(e.TextDocument.FilePath, out var document))
            {
                document.DocumentRenamed -= DocumentRenamed;
                document.Dispose();
                _documents.Remove(e.TextDocument.FilePath);
                DocumentDisposed?.Invoke(document);
            }
        }

        private void DocumentRenamed(IDocument document, string oldPath, string newPath)
        {
            if (_documents.Remove(oldPath))
                _documents.Add(newPath, document);
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window LostFocus)
        {
            if (GotFocus.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
            {
                var openWindowPath = System.IO.Path.Combine(GotFocus.Document.Path, GotFocus.Document.Name);
                _documents.TryGetValue(openWindowPath, out var document);

                // if this document is opened for the first time, then it can be a RadeonAsm document, but 
                // the parser is initialized after visual buffer initialization, so
                // it is necessary to force initialize RadeonAsm document
                if (document == null)
                {
                    var vsTextBuffer = Utils.GetWindowVisualBuffer(GotFocus, _serviceProvider.ServiceProvider);
                    if (vsTextBuffer != null)
                    {
                        var textBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDocumentBuffer(vsTextBuffer);
                        document = GetOrCreateDocument(textBuffer);
                    }
                }
                ActiveDocumentChanged?.Invoke(document);
            }
        }
    }
}
