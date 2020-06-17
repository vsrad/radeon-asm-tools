using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser.RadAsm
{
    public class AsmLexer : ILexer
    {
        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsmLexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsmLexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) =>
            _tt[type];

        private static readonly Dictionary<int, RadAsmTokenType> _tt = new Dictionary<int, RadAsmTokenType>()
        {
            { RadAsmLexer.EQ, RadAsmTokenType.Operation },
            { RadAsmLexer.LT, RadAsmTokenType.Operation },
            { RadAsmLexer.LE, RadAsmTokenType.Operation },
            { RadAsmLexer.EQEQ, RadAsmTokenType.Operation },
            { RadAsmLexer.NE, RadAsmTokenType.Operation },
            { RadAsmLexer.GE, RadAsmTokenType.Operation },
            { RadAsmLexer.GT, RadAsmTokenType.Operation },
            { RadAsmLexer.ANDAND, RadAsmTokenType.Operation },
            { RadAsmLexer.OROR, RadAsmTokenType.Operation },
            { RadAsmLexer.NOT, RadAsmTokenType.Operation },
            { RadAsmLexer.TILDE, RadAsmTokenType.Operation },
            { RadAsmLexer.PLUS, RadAsmTokenType.Operation },
            { RadAsmLexer.MINUS, RadAsmTokenType.Operation },
            { RadAsmLexer.STAR, RadAsmTokenType.Operation },
            { RadAsmLexer.SLASH, RadAsmTokenType.Operation },
            { RadAsmLexer.PERCENT, RadAsmTokenType.Operation },
            { RadAsmLexer.CARET, RadAsmTokenType.Operation },
            { RadAsmLexer.AND, RadAsmTokenType.Operation },
            { RadAsmLexer.OR, RadAsmTokenType.Operation },
            { RadAsmLexer.SHL, RadAsmTokenType.Operation },
            { RadAsmLexer.SHR, RadAsmTokenType.Operation },
            { RadAsmLexer.BINOP, RadAsmTokenType.Operation },

            { RadAsmLexer.TEXT, RadAsmTokenType.Keyword },
            { RadAsmLexer.SET, RadAsmTokenType.Keyword },
            { RadAsmLexer.BYTE, RadAsmTokenType.Keyword },
            { RadAsmLexer.SHORT, RadAsmTokenType.Keyword },
            { RadAsmLexer.LONG, RadAsmTokenType.Keyword },
            { RadAsmLexer.EXITM, RadAsmTokenType.Keyword },
            { RadAsmLexer.INCLUDE, RadAsmTokenType.Keyword },
            { RadAsmLexer.ALTMAC, RadAsmTokenType.Keyword },
            { RadAsmLexer.NOALTMAC, RadAsmTokenType.Keyword },
            { RadAsmLexer.LOCAL, RadAsmTokenType.Keyword },
            { RadAsmLexer.LINE, RadAsmTokenType.Keyword },
            { RadAsmLexer.SIZE, RadAsmTokenType.Keyword },
            { RadAsmLexer.LN, RadAsmTokenType.Keyword },
            { RadAsmLexer.NOPS, RadAsmTokenType.Keyword },
            { RadAsmLexer.ERROR, RadAsmTokenType.Keyword },
            { RadAsmLexer.END, RadAsmTokenType.Keyword },

            { RadAsmLexer.MACRO, RadAsmTokenType.Keyword },
            { RadAsmLexer.ENDM, RadAsmTokenType.Keyword },

            { RadAsmLexer.IF, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFDEF, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNDEF, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNOTDEF, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFB, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFC, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFEQ, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFEQS, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFGE, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFGT, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFLE, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFLT, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNB, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNC, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNE, RadAsmTokenType.Keyword },
            { RadAsmLexer.IFNES, RadAsmTokenType.Keyword },
            { RadAsmLexer.STARTIF, RadAsmTokenType.Keyword },
            { RadAsmLexer.ELSEIF, RadAsmTokenType.Keyword },
            { RadAsmLexer.ELSE, RadAsmTokenType.Keyword },
            { RadAsmLexer.MIDDLEIF, RadAsmTokenType.Keyword },
            { RadAsmLexer.ENDIF, RadAsmTokenType.Keyword },

            { RadAsmLexer.REPT, RadAsmTokenType.Keyword },
            { RadAsmLexer.ENDR, RadAsmTokenType.Keyword },
            { RadAsmLexer.IRP, RadAsmTokenType.Keyword },
            { RadAsmLexer.IRPC, RadAsmTokenType.Keyword },
            { RadAsmLexer.DEF, RadAsmTokenType.Keyword },
            { RadAsmLexer.ENDEF, RadAsmTokenType.Keyword },

            { RadAsmLexer.HSA_CO_VERSION, RadAsmTokenType.Keyword },
            { RadAsmLexer.HSA_CO_ISA, RadAsmTokenType.Keyword },
            { RadAsmLexer.AMD_HSA_KERNEL, RadAsmTokenType.Keyword },
            { RadAsmLexer.AMD_KERNEL_CODE, RadAsmTokenType.Keyword },
            { RadAsmLexer.AMD_END_KERNEL_CODE, RadAsmTokenType.Keyword },

            { RadAsmLexer.PP_INCLUDE, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_DEFINE, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_UNDEF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_PRAGMA, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_ERROR, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_WARNING, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_IMPORT, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_LINE, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_INCLUDE_NEXT, RadAsmTokenType.Preprocessor },

            { RadAsmLexer.PP_IF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_IFDEF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_IFNDEF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_ELSE, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_ELSIF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_ELIF, RadAsmTokenType.Preprocessor },
            { RadAsmLexer.PP_ENDIF, RadAsmTokenType.Preprocessor },

            { RadAsmLexer.COMMA, RadAsmTokenType.Structural },
            { RadAsmLexer.SEMI, RadAsmTokenType.Structural },
            { RadAsmLexer.COLON, RadAsmTokenType.Structural },
            { RadAsmLexer.LPAREN, RadAsmTokenType.Structural },
            { RadAsmLexer.RPAREN, RadAsmTokenType.Structural },
            { RadAsmLexer.LSQUAREBRACKET, RadAsmTokenType.Structural },
            { RadAsmLexer.RSQUAREBRACKET, RadAsmTokenType.Structural },
            { RadAsmLexer.LCURVEBRACKET, RadAsmTokenType.Structural },
            { RadAsmLexer.RCURVEBRACKET, RadAsmTokenType.Structural },

            { RadAsmLexer.CONSTANT, RadAsmTokenType.Number },
            { RadAsmLexer.STRING_LITERAL, RadAsmTokenType.String },
            { RadAsmLexer.IDENTIFIER, RadAsmTokenType.Identifier },

            { RadAsmLexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsmLexer.EOL, RadAsmTokenType.Whitespace },

            { RadAsmLexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsmLexer.LINE_COMMENT, RadAsmTokenType.Comment },
            { RadAsmLexer.UNKNOWN, RadAsmTokenType.Unknown },
        };

        public int IDENTIFIER => RadAsmLexer.IDENTIFIER;
        public int LINE_COMMENT => RadAsmLexer.LINE_COMMENT;
        public int BLOCK_COMMENT => RadAsmLexer.BLOCK_COMMENT;
        public int LPAREN => RadAsmLexer.LPAREN;
        public int RPAREN => RadAsmLexer.RPAREN;
        public int LSQUAREBRACKET => RadAsmLexer.LSQUAREBRACKET;
        public int RSQUAREBRACKET => RadAsmLexer.RSQUAREBRACKET;
        public int LCURVEBRACKET => RadAsmLexer.LCURVEBRACKET;
        public int RCURVEBRACKET => RadAsmLexer.RCURVEBRACKET;
    }
}
