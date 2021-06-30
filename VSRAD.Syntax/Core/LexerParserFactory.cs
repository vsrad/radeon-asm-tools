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
        private static LexerParser? GetLexerParser(AsmType asmType)
        {

            switch (asmType)
            {
                case AsmType.RadAsm:
                    return new LexerParser()
                    {
                        Lexer = Asm1Lexer.Instance,
                        Parser = Asm1Parser.Instance
                    };
                case AsmType.RadAsm2:
                    return new LexerParser()
                    {
                        Lexer = Asm2Lexer.Instance,
                        Parser = Asm2Parser.Instance
                    };
                case AsmType.RadAsmDoc:
                    return new LexerParser()
                    {
                        Lexer = AsmDocLexer.Instance,
                        Parser = AsmDocParser.Instance
                    };
                default: return null;
            }
        }
    }
}
