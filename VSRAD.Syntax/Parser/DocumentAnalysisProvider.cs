using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
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
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly InstructionListManager _instructionListManager;

        [ImportingConstructor]
        public DocumentAnalysisProvoder(RadeonServiceProvider serviceProvider, InstructionListManager instructionListManager)
        {
            _serviceProvider = serviceProvider;
            _instructionListManager = instructionListManager;
        }

        public DocumentAnalysis CreateDocumentAnalysis(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty<DocumentAnalysis>(typeof(DocumentAnalysis), out var documentAnalysis))
                return documentAnalysis;

            if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument))
                return CreateDocumentAnalysis(textDocument);

            throw new ArgumentException("Cannot create docuement analysis");
        }

        public DocumentAnalysis GetOrCreateDocumentAnalysis(string filepath)
        {
            // return DocumentAnalysis only if file is open in the visual studio
            if (Utils.IsDocumentOpen(_serviceProvider.ServiceProvider, filepath, out var vsTextBuffer))
            {
                var docBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDocumentBuffer(vsTextBuffer);
                return CreateDocumentAnalysis(docBuffer);
            }

            return null;
        }

        private DocumentAnalysis CreateDocumentAnalysis(ITextDocument textDocument)
        {
            var buffer = textDocument.TextBuffer;
            var asmType = buffer.CurrentSnapshot.GetAsmType();

            DocumentAnalysis documentAnalysis;
            if (asmType == AsmType.RadAsm2)
                documentAnalysis = new DocumentAnalysis(new Asm2Lexer(), new Asm2Parser(this), buffer, _instructionListManager);
            else if (asmType == AsmType.RadAsmDoc)
                documentAnalysis = new DocumentAnalysis(new AsmDocLexer(), new AsmDocParser(this), buffer, _instructionListManager);
            else
                documentAnalysis = new DocumentAnalysis(new AsmLexer(), new AsmParser(new DocumentInfo(textDocument), this), buffer, _instructionListManager);

            buffer.Properties.AddProperty(typeof(DocumentAnalysis), documentAnalysis);
            documentAnalysis.Initialize();
            return documentAnalysis;
        }
    }
}
