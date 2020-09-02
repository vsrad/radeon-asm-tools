using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.RadAsm;
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
            _serviceProvider.TextDocumentFactoryService.TextDocumentDisposed += DocumentDisposed;
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

            return CreateDocument(textDocument);
        }

        public IDocument GetOrCreateDocument(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(typeof(IDocument), out IDocument document))
                return document;

            var textDocument = buffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            return CreateDocument(textDocument);
        }

        private IDocument CreateDocument(ITextDocument textDocument)
        {
            ILexer lexer;
            IParser parser;
            var contentType = textDocument.TextBuffer.ContentType;
            if (contentType == _contentTypeManager.Asm1ContentType)
            {
                lexer = Asm1Lexer;
                parser = Asm1Parser;
            }
            else if (contentType == _contentTypeManager.Asm2ContentType)
            {
                lexer = Asm2Lexer;
                parser = Asm2Parser;
            }
            else if (contentType == _contentTypeManager.AsmDocContentType)
            {
                lexer = AsmDocLexer;
                parser = AsmDocParser;
            }
            else return null;

            var document = new Document(textDocument, lexer, parser);
            document.DocumentRenamed += DocumentRenamed;

            textDocument.TextBuffer.Properties.AddProperty(typeof(IDocument), document);
            _documents.Add(document.Path, document);
            return document;
        }

        private void DocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (_documents.ContainsKey(e.TextDocument.FilePath))
                _documents.Remove(e.TextDocument.FilePath);
        }

        private void DocumentRenamed(IDocument document, string oldPath, string newPath)
        {
            _documents.Remove(oldPath);
            _documents.Add(newPath, document);
        }
    }
}
