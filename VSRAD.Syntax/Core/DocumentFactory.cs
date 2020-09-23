using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    [Export(typeof(IDocumentFactory))]
    internal class DocumentFactory : IDocumentFactory
    {
        private readonly ContentTypeManager _contentTypeManager;
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly Dictionary<string, IDocument> _documents;

        #region parsers
        private readonly IParser Asm1Parser;
        private readonly IParser Asm2Parser;
        private readonly IParser AsmDocParser;
        #endregion

        #region lexers
        private static readonly ILexer Asm1Lexer = new AsmLexer();
        private static readonly ILexer Asm2Lexer = new Asm2Lexer();
        private static readonly ILexer AsmDocLexer = new AsmDocLexer();
        #endregion

        public event ActiveDocumentChangedEventHandler ActiveDocumentChanged;
        public event DocumentCreatedEventHandler DocumentCreated;
        public event DocumentDisposedEventHandler DocumentDisposed;

        [ImportingConstructor]
        DocumentFactory(RadeonServiceProvider serviceProvider,
            ContentTypeManager contentTypeManager,
            IInstructionListManager instructionManager)
        {
            Asm1Parser = new Asm1Parser(this, instructionManager);
            Asm2Parser = new Asm2Parser(this, instructionManager);
            AsmDocParser = new AsmDocParser(this);

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

            return CreateDocument<InvisibleDocument>(textDocument);
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
                document = CreateDocument<Document>(textDocument);
            }

            DocumentCreated?.Invoke(document);
            return document;
        }

        private IDocument CreateDocument<T>(ITextDocument textDocument) where T : Document, new()
        {
            var lexerParser = GetLexerParser(textDocument.TextBuffer.ContentType);
            if (!lexerParser.HasValue)
                return null;

            var lexer = lexerParser.Value.Item1;
            var parser = lexerParser.Value.Item2;

            var document = new T();
            document.Initialize(textDocument, lexer, parser);
            ObserveDocument(document, textDocument);

            return document;
        }

        private void ObserveDocument(IDocument document, ITextDocument textDocument)
        {
            document.DocumentRenamed += DocumentRenamed;
            textDocument.TextBuffer.Properties.AddProperty(typeof(IDocument), document);

            _documents.Add(document.Path, document);
        }

        private (ILexer, IParser)? GetLexerParser(IContentType contentType)
        {
            if (contentType == _contentTypeManager.Asm1ContentType)
                return (Asm1Lexer, Asm1Parser);
            else if (contentType == _contentTypeManager.Asm2ContentType)
                return (Asm2Lexer, Asm2Parser);
            else if (contentType == _contentTypeManager.AsmDocContentType)
                return (AsmDocLexer, AsmDocParser);

            else return null;
        }

        private void TextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (_documents.ContainsKey(e.TextDocument.FilePath))
            {
                if (_documents.TryGetValue(e.TextDocument.FilePath, out var document))
                {
                    document.DocumentRenamed -= DocumentRenamed;
                    document.Dispose();
                    _documents.Remove(e.TextDocument.FilePath);
                    DocumentDisposed?.Invoke(document);
                }
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
                ActiveDocumentChanged?.Invoke(document);
            }
        }
    }
}
