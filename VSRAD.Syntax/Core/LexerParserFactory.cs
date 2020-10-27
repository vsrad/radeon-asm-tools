using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Core
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct LexerParser
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ILexer Lexer;
        public IParser Parser;
    }

    internal partial class DocumentFactory
    {
        private LexerParser? GetLexerParser(AsmType asmType)
        {
            var instructionManager = _instructionManager.Value;

            switch (asmType)
            {
                case AsmType.RadAsm:
                    return new LexerParser()
                    {
                        Lexer = new AsmLexer(),
                        Parser = new Asm1Parser(this, instructionManager)
                    };
                case AsmType.RadAsm2:
                    return new LexerParser()
                    {
                        Lexer = new Asm2Lexer(),
                        Parser = new Asm2Parser(this, instructionManager)
                    };
                case AsmType.RadAsmDoc:
                    return new LexerParser()
                    {
                        Lexer = new AsmDocLexer(),
                        Parser = new AsmDocParser()
                    };
                default: return null;
            }
        }
    }
}
