using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser.RadAsm;
using VSRAD.Syntax.Parser.RadAsm2;
using VSRAD.Syntax.Parser.RadAsmDoc;

namespace VSRAD.Syntax.Parser
{
    [Export]
    internal class DocumentAnalysisProvoder
    {
        private readonly InstructionListManager _instructionListManager;
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public DocumentAnalysisProvoder(IDocumentFactory documentFactory, InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
            _documentFactory = documentFactory;
        }

        public DocumentAnalysis CreateDocumentAnalysis(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty<DocumentAnalysis>(typeof(DocumentAnalysis), out var documentAnalysis))
                return documentAnalysis;

            var textDocument = _documentFactory.GetDocument(buffer);
            return CreateDocumentAnalysis(textDocument);
        }

        public DocumentAnalysis GetOrCreateDocumentAnalysis(string filepath)
        {
            var textDocument = _documentFactory.GetDocument(filepath);
            if (textDocument.TextBuffer.Properties.TryGetProperty<DocumentAnalysis>(typeof(DocumentAnalysis), out var documentAnalysis))
                return documentAnalysis;

            return CreateDocumentAnalysis(textDocument);
        }

        private DocumentAnalysis CreateDocumentAnalysis(DocumentInfo document)
        {
            var buffer = document.TextBuffer;
            var asmType = buffer.CurrentSnapshot.GetAsmType();

            DocumentAnalysis documentAnalysis;
            switch (asmType)
            {
                case AsmType.RadAsm:
                    documentAnalysis = new DocumentAnalysis(new AsmLexer(), new AsmParser(document, this), buffer, _instructionListManager);
                    break;
                case AsmType.RadAsm2:
                    documentAnalysis = new DocumentAnalysis(new Asm2Lexer(), new Asm2Parser(document, this), buffer, _instructionListManager);
                    break;
                case AsmType.RadAsmDoc:
                    documentAnalysis = new DocumentAnalysis(new AsmDocLexer(), new AsmDocParser(this), buffer, _instructionListManager);
                    break;
                default:
                    throw new ArgumentException($"Cannot create DocumentAnalysis for {document}, it does not belong to RadeonAsm file type");
            }

            buffer.Properties.AddProperty(typeof(DocumentAnalysis), documentAnalysis);
            documentAnalysis.Initialize();
            return documentAnalysis;
        }
    }
}
