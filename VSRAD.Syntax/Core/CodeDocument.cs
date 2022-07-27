using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    internal class CodeDocument : Document
    {
        private readonly IInstructionListManager _instructionListManager;

        public CodeDocument(IInstructionListManager instructionListManager, ITextDocument textDocument, ILexer lexer, IParser parser)
            : base(textDocument, lexer, parser)
        {
            _instructionListManager = instructionListManager;
            _instructionListManager.InstructionsUpdated += InstructionsUpdated;
        }

        public override void Dispose()
        {
            base.Dispose();
            _instructionListManager.InstructionsUpdated -= InstructionsUpdated;
        }

        private void InstructionsUpdated(IInstructionListManager manager, AsmType asmType) =>
            DocumentTokenizer.Rescan(RescanReason.InstructionsChanged);
    }
}
