using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Lexer
{
    public class Asm1Lexer : ILexer
    {
        public static ILexer Instance = new Asm1Lexer();

        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsm1Lexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsm1Lexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) =>
            _tt[type];

        private static readonly Dictionary<int, RadAsmTokenType> _tt = new Dictionary<int, RadAsmTokenType>()
        {
            { RadAsm1Lexer.TEXT, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.SET, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.BYTE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.SHORT, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.LONG, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.EXITM, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.INCLUDE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ALTMAC, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.NOALTMAC, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.LOCAL, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.LINE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.SIZE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.LN, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.NOPS, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ERROR, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.END, RadAsmTokenType.Keyword },

            { RadAsm1Lexer.MACRO, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ENDM, RadAsmTokenType.Keyword },

            { RadAsm1Lexer.IF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFDEF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNDEF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNOTDEF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFB, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFC, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFEQ, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFEQS, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFGE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFGT, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFLE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFLT, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNB, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNC, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IFNES, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ELSEIF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ELSE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ENDIF, RadAsmTokenType.Keyword },

            { RadAsm1Lexer.REPT, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ENDR, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IRP, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.IRPC, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.DEF, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.ENDEF, RadAsmTokenType.Keyword },

            { RadAsm1Lexer.HSA_CO_VERSION, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.HSA_CO_ISA, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.AMD_HSA_KERNEL, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.AMD_KERNEL_CODE, RadAsmTokenType.Keyword },
            { RadAsm1Lexer.AMD_END_KERNEL_CODE, RadAsmTokenType.Keyword },

            { RadAsm1Lexer.PP_INCLUDE, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_DEFINE, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_UNDEF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_PRAGMA, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_ERROR, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_WARNING, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_IMPORT, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_LINE, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_INCLUDE_NEXT, RadAsmTokenType.Preprocessor },

            { RadAsm1Lexer.PP_IF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_IFDEF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_IFNDEF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_ELSE, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_ELSIF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_ELIF, RadAsmTokenType.Preprocessor },
            { RadAsm1Lexer.PP_ENDIF, RadAsmTokenType.Preprocessor },

            { RadAsm1Lexer.LE, RadAsmTokenType.Operation },
            { RadAsm1Lexer.EQEQ, RadAsmTokenType.Operation },
            { RadAsm1Lexer.NE, RadAsmTokenType.Operation },
            { RadAsm1Lexer.GE, RadAsmTokenType.Operation },
            { RadAsm1Lexer.LOGAND, RadAsmTokenType.Operation },
            { RadAsm1Lexer.LOGOR, RadAsmTokenType.Operation },
            { RadAsm1Lexer.SHL, RadAsmTokenType.Operation },
            { RadAsm1Lexer.SHR, RadAsmTokenType.Operation },
            { RadAsm1Lexer.EQ, RadAsmTokenType.Operation },
            { RadAsm1Lexer.LT, RadAsmTokenType.Operation },
            { RadAsm1Lexer.GT, RadAsmTokenType.Operation },
            { RadAsm1Lexer.NOT, RadAsmTokenType.Operation },
            { RadAsm1Lexer.TILDE, RadAsmTokenType.Operation },
            { RadAsm1Lexer.PLUS, RadAsmTokenType.Operation },
            { RadAsm1Lexer.MINUS, RadAsmTokenType.Operation },
            { RadAsm1Lexer.PROD, RadAsmTokenType.Operation },
            { RadAsm1Lexer.DIV, RadAsmTokenType.Operation },
            { RadAsm1Lexer.MOD, RadAsmTokenType.Operation },
            { RadAsm1Lexer.BITXOR, RadAsmTokenType.Operation },
            { RadAsm1Lexer.BITAND, RadAsmTokenType.Operation },
            { RadAsm1Lexer.BITOR, RadAsmTokenType.Operation },

            { RadAsm1Lexer.COMMA, RadAsmTokenType.Comma },
            { RadAsm1Lexer.SEMI, RadAsmTokenType.Semi },
            { RadAsm1Lexer.COLON, RadAsmTokenType.Colon },
            { RadAsm1Lexer.LPAREN, RadAsmTokenType.Lparen },
            { RadAsm1Lexer.RPAREN, RadAsmTokenType.Rparen },
            { RadAsm1Lexer.LSQUAREBRACKET, RadAsmTokenType.LsquareBracket },
            { RadAsm1Lexer.RSQUAREBRACKET, RadAsmTokenType.RsquareBracket },
            { RadAsm1Lexer.LCURVEBRACKET, RadAsmTokenType.LcurveBracket },
            { RadAsm1Lexer.RCURVEBRACKET, RadAsmTokenType.RcurveBracket },

            { RadAsm1Lexer.CONSTANT, RadAsmTokenType.Number },
            { RadAsm1Lexer.STRING_LITERAL, RadAsmTokenType.String },
            { RadAsm1Lexer.IDENTIFIER, RadAsmTokenType.Identifier },

            { RadAsm1Lexer.LINE_COMMENT, RadAsmTokenType.Comment },
            { RadAsm1Lexer.BLOCK_COMMENT, RadAsmTokenType.Comment },

            { RadAsm1Lexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsm1Lexer.EOL, RadAsmTokenType.Whitespace },
            { RadAsm1Lexer.UNKNOWN, RadAsmTokenType.Unknown },
        };
    }
}
