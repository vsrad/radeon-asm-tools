using System.Collections.Generic;
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
        private Dictionary<AsmType, LexerParser> _asmSpecificLexerParser;

        private LexerParser? GetLexerParser(AsmType asmType)
        {
            InitializeLexerParser();

            if (_asmSpecificLexerParser.TryGetValue(asmType, out var lexerParser))
                return lexerParser;

            return null;
        }

        private void InitializeLexerParser()
        {
            if (_asmSpecificLexerParser != null)
                return;

            _asmSpecificLexerParser = new Dictionary<AsmType, LexerParser>()
            {
                {
                    AsmType.RadAsm,
                    new LexerParser()
                    {
                        Lexer = Asm1Lexer.Instance,
                        Parser = Asm1Parser.Instance
                    }
                },
                {
                    AsmType.RadAsm2,
                    new LexerParser()
                    {
                        Lexer = Asm2Lexer.Instance,
                        Parser = Asm2Parser.Instance
                    }
                },
                {
                    AsmType.RadAsmDoc,
                    new LexerParser()
                    {
                        Lexer = AsmDocLexer.Instance,
                        Parser = AsmDocParser.Instance
                    }
                }
            };
        }
    }
}
