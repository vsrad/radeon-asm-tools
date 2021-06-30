using EnvDTE;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
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
        private readonly Lazy<IInvisibleTextDocumentFactory> _invisibleDocumentFactory;


        public event ActiveDocumentChangedEventHandler ActiveDocumentChanged;
        public event DocumentCreatedEventHandler DocumentCreated;
        public event DocumentDisposedEventHandler DocumentDisposed;

        [ImportingConstructor]
        public DocumentFactory(RadeonServiceProvider serviceProvider,
            ContentTypeManager contentTypeManager,
            Lazy<IInstructionListManager> instructionManager,
            Lazy<IInvisibleTextDocumentFactory> invisibleDocumentFactory)
        {
            _instructionManager = instructionManager;
            _invisibleDocumentFactory = invisibleDocumentFactory;

            _documents = new Dictionary<string, IDocument>(StringComparer.OrdinalIgnoreCase);
            _contentTypeManager = contentTypeManager;
            _serviceProvider = serviceProvider;
            _serviceProvider.TextDocumentFactoryService.TextDocumentDisposed += TextDocumentDisposed;

            var dte = _serviceProvider.ServiceProvider.GetService(typeof(DTE)) as DTE;
            dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
        }

        public IDocument GetOrCreateDocument(string path)
        {
            var fullPath = Path.GetFullPath(path);

            if (_documents.TryGetValue(fullPath, out var document))
                return document;

            return File.Exists(fullPath)
                ? CreateDocument(fullPath)
                : null;
        }

        private IDocument CreateDocument(string path)
        {
            var contentType = _contentTypeManager.DetermineContentType(path);
            if (contentType == null)
                return null;

            var textDocument = CustomThreadHelper.RunOnMainThread(() =>
                _invisibleDocumentFactory.Value.CreateAndLoadTextDocument(path, contentType));

            var factory = GetDocumentFactory(textDocument);
            if (factory == null) return null;

            return CreateDocument(textDocument, factory);
        }

        public IDocument GetOrCreateDocument(ITextBuffer buffer)
        {
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument))
                return null;

            if (_documents.TryGetValue(textDocument.FilePath, out var document))
                return document;

            var factory = GetDocumentFactory(textDocument);
            if (factory == null) return null;

            document = CreateDocument(textDocument, factory);

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
                    return (lexer, parser) => new CodeDocument(_instructionManager.Value, document, lexer, parser);
                case AsmType.RadAsmDoc:
                    return (lexer, parser) => new Document(document, lexer, parser);
                default:
                    return null;
            }
        }

        private IDocument CreateDocument(ITextDocument textDocument, Func<ILexer, IParser, IDocument> creator)
        {
            var lexerParser = GetLexerParser(textDocument.TextBuffer.GetAsmType());
            if (!lexerParser.HasValue) return null;

            var document = creator(lexerParser.Value.Lexer, lexerParser.Value.Parser);
            ObserveDocument(document);

            return document;
        }

        private void ObserveDocument(IDocument document)
        {
            document.DocumentRenamed += DocumentRenamed;
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
