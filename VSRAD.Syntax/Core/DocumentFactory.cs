using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.RadAsm;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.Core
{
    [Export(typeof(IDocumentFactory))]
    internal class DocumentFactory : IDocumentFactory
    {
        private readonly ContentTypeManager _contentTypeManager;
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
        DocumentFactory(ContentTypeManager contentTypeManager)
        {
            Asm1Parser = new Asm1Parser(this);
            Asm2Parser = new Asm2Parser(this);
            AsmDocParser = new AsmDocParser(this);
            _documents = new Dictionary<string, IDocument>();
            _contentTypeManager = contentTypeManager;
        }

        public IDocument GetOrCreateDocument(string path)
        {
            //if (Utils.IsDocumentOpen(_serviceProvider.ServiceProvider, path, out var vsTextBuffer))
            //{
            //    var textBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDataBuffer(vsTextBuffer);
            //    return GetOrCreateDocument(textBuffer);
            //}

            //var contentType = _contentTypeManager.DetermineContentType(path)
            //    ?? throw new ArgumentException($"File {path} do not belog to asm1 or asm2 or asmdoc");

            //var textDocument = _serviceProvider.TextDocumentFactoryService.CreateAndLoadTextDocument(path, contentType);

            //return GetOrCreateDocumentInfo(textDocument);
            throw new NotImplementedException();
        }

        public IDocument GetOrCreateDocument(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(typeof(IDocument), out IDocument document))
                return document;

            var textDocument = buffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            if (_documents.TryGetValue(textDocument.FilePath, out document))
            {
                buffer.Properties.AddProperty(typeof(IDocument), document);
                return document;
            }

            return CreateDocument(textDocument);
        }

        private IDocument CreateDocument(ITextDocument textDocument)
        {
            // TODO: select specific lexer and parser
            var lexer = Asm1Lexer;
            var parser = Asm1Parser;

            var document = new Document(textDocument, lexer, parser);
            _documents.Add(document.Path, document);
            document.DocumentRenamed += DocumentRenamed;

            return document;
        }

        private void DocumentRenamed(IDocument document, string oldPath, string newPath)
        {
            _documents.Remove(oldPath);
            _documents.Add(newPath, document);
        }
    }
}
