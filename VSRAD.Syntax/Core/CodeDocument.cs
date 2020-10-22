using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    internal class CodeDocument : Document
    {
        public CodeDocument(IInstructionListManager instructionListManager, ITextDocument textDocument, ILexer lexer, IParser parser)
            : base(textDocument, lexer, parser)
        {
            instructionListManager.InstructionsUpdated += (s, t) => DocumentTokenizer.Rescan();
        }
    }
}
