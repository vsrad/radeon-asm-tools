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
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly Dictionary<string, KeyValuePair<ITextDocument, IDocument>> _documents;
        private readonly Lazy<ContentTypeManager> _contentTypeManager;
        private readonly Lazy<IInstructionListManager> _instructionManager;


        public event ActiveDocumentChangedEventHandler ActiveDocumentChanged;
        public event DocumentCreatedEventHandler DocumentCreated;
        public event DocumentDisposedEventHandler DocumentDisposed;

        [ImportingConstructor]
        public DocumentFactory(RadeonServiceProvider serviceProvider,
            Lazy<ContentTypeManager> contentTypeManager,
            Lazy<IInstructionListManager> instructionManager)
        {
            _instructionManager = instructionManager;

            _documents = new Dictionary<string, KeyValuePair<ITextDocument, IDocument>>(StringComparer.OrdinalIgnoreCase);
            _contentTypeManager = contentTypeManager;
            _serviceProvider = serviceProvider;

            var dte = _serviceProvider.ServiceProvider.GetService(typeof(DTE)) as DTE;
            dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
        }

        public IDocument GetOrCreateDocument(string path)
        {
            if (_documents.TryGetValue(path, out var document))
                return document.Value;

            if (!System.IO.File.Exists(path))
                return null;

            var contentType = _contentTypeManager.Value.DetermineContentType(path);
            if (contentType == null)
                return null;

            var textDocument = _serviceProvider
                .TextDocumentFactoryService
                .CreateAndLoadTextDocument(path, contentType);

            return GetOrCreateDocument(textDocument);
        }

        public IDocument GetOrCreateDocument(ITextBuffer buffer)
        {
            if (!buffer.GetTextDocument(out var textDocument))
                return null;
            if (_documents.TryGetValue(textDocument.FilePath, out var documentPair) && textDocument == documentPair.Key)
                return documentPair.Value;

            return GetOrCreateDocument(textDocument);
        }

        private IDocument GetOrCreateDocument(ITextDocument textDocument)
        {
            var factory = GetDocumentFactory(textDocument);
            if (factory == null) return null;

            var document = CreateDocument(textDocument, factory);
            // CreateDocument can return null if document does not belong to RadAsmSyntax
            if (document != null) DocumentCreated?.Invoke(document);
            return document;
        }

        private Func<ILexer, IParser, IDocument> GetDocumentFactory(ITextDocument document)
        {
            switch (document.TextBuffer.GetAsmType())
            {
                case AsmType.RadAsm:
                case AsmType.RadAsm2:
                    return (lexer, parser) => new CodeDocument(_serviceProvider.TextDocumentFactoryService, _instructionManager.Value, document, lexer, parser, OnDocumentDestroy);
                case AsmType.RadAsmDoc:
                    return (lexer, parser) => new Document(_serviceProvider.TextDocumentFactoryService, document, lexer, parser, OnDocumentDestroy);
                default:
                    return null;
            }
        }

        private IDocument CreateDocument(ITextDocument textDocument, Func<ILexer, IParser, IDocument> creator)
        {
            var lexerParser = GetLexerParser(textDocument.TextBuffer.GetAsmType());
            if (!lexerParser.HasValue) return null;

            IDocument document;
            if (_documents.TryGetValue(textDocument.FilePath, out var documentPair))
            {
                document = documentPair.Value;
                document.ReplaceDocument(textDocument);
                OnDocumentDestroy(document);
            }
            else
            {
                document = creator(lexerParser.Value.Lexer, lexerParser.Value.Parser);
            }

            textDocument.FileActionOccurred += TextDocumentActionOccurred;
            _documents.Add(document.Path, new KeyValuePair<ITextDocument, IDocument>(textDocument, document));
            return document;
        }

        private void TextDocumentActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType != FileActionTypes.DocumentRenamed)
                return;

            foreach (var path in _documents.Keys)
            {
                var documentPair = _documents[path];
                if (!documentPair.Key.Equals(sender))
                    continue;

                _documents[e.FilePath] = documentPair;
                _documents.Remove(path);
                break;
            }
        }

        private void OnDocumentDestroy(IDocument document)
        {
            if (!_documents.TryGetValue(document.Path, out var documentPair))
                return;

            documentPair.Key.FileActionOccurred -= TextDocumentActionOccurred;
            _documents.Remove(document.Path);
            DocumentDisposed?.Invoke(document);
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window _)
        {
            if (GotFocus.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
            {
                IDocument document = null;
                var openWindowPath = System.IO.Path.Combine(GotFocus.Document.Path, GotFocus.Document.Name);
                if (_documents.TryGetValue(openWindowPath, out var documentPair))
                {
                    document = documentPair.Value;

                    // if this document is opened for the first time, then it can be a RadeonAsm document, but 
                    // the parser is initialized after visual buffer initialization, so
                    // it is necessary to force initialize RadeonAsm document
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
