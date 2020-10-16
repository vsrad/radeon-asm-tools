using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    internal class CodeDocument : Document
    {
        private readonly ICodeParser _codeParser;

        public CodeDocument(IInstructionListManager instructionListManager, ITextDocument textDocument, ILexer lexer, ICodeParser parser)
            : base(textDocument, lexer, parser)
        {
            _codeParser = parser;
            instructionListManager.InstructionsUpdated += InstructionUpdated;
            InstructionUpdated(instructionListManager, AsmType.RadAsmCode);
        }

        private void RescanDocument() => DocumentTokenizer.Rescan();

        private void InstructionUpdated(IInstructionListManager sender, AsmType asmType)
        {
            var documentType = CurrentSnapshot.GetAsmType();
            var expected = asmType & documentType;

            if (expected == documentType)
            {
                var instructions = sender.GetInstructions(documentType);
                var selectedSetInstructions = sender.GetSelectedSetInstructions(documentType);

                _codeParser.UpdateInstructions(instructions, selectedSetInstructions);
                RescanDocument();
            }
        }
    }
}
