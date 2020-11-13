using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;


namespace VSRAD.Syntax.Core.Lexer
{
    public class Asm2Lexer : ILexer
    {
        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsm2Lexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsm2Lexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) =>
            _tt[type];

        private static readonly Dictionary<int, RadAsmTokenType> _tt = new Dictionary<int, RadAsmTokenType>()
        {
            { RadAsm2Lexer.EQ, RadAsmTokenType.Operation },
            { RadAsm2Lexer.LT, RadAsmTokenType.Operation },
            { RadAsm2Lexer.LE, RadAsmTokenType.Operation },
            { RadAsm2Lexer.EQEQ, RadAsmTokenType.Operation },
            { RadAsm2Lexer.NE, RadAsmTokenType.Operation },
            { RadAsm2Lexer.GE, RadAsmTokenType.Operation },
            { RadAsm2Lexer.GT, RadAsmTokenType.Operation },
            { RadAsm2Lexer.ANDAND, RadAsmTokenType.Operation },
            { RadAsm2Lexer.OROR, RadAsmTokenType.Operation },
            { RadAsm2Lexer.NOT, RadAsmTokenType.Operation },
            { RadAsm2Lexer.TILDE, RadAsmTokenType.Operation },
            { RadAsm2Lexer.PLUS, RadAsmTokenType.Operation },
            { RadAsm2Lexer.MINUS, RadAsmTokenType.Operation },
            { RadAsm2Lexer.STAR, RadAsmTokenType.Operation },
            { RadAsm2Lexer.SLASH, RadAsmTokenType.Operation },
            { RadAsm2Lexer.PERCENT, RadAsmTokenType.Operation },
            { RadAsm2Lexer.CARET, RadAsmTokenType.Operation },
            { RadAsm2Lexer.AND, RadAsmTokenType.Operation },
            { RadAsm2Lexer.OR, RadAsmTokenType.Operation },
            { RadAsm2Lexer.SHL, RadAsmTokenType.Operation },
            { RadAsm2Lexer.SHR, RadAsmTokenType.Operation },
            { RadAsm2Lexer.BINOP, RadAsmTokenType.Operation },

            { RadAsm2Lexer.VAR, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.VMCNT, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.EXPCNT, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.LGKMCNT, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.HWREG, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.SENDMSG, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.ASIC, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.TYPE, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.ASSERT, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.FUNCTION, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.IF, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.ELSIF, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.ELSE, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.FOR, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.WHILE, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.END, RadAsmTokenType.Keyword },

            { RadAsm2Lexer.REPEAT, RadAsmTokenType.Keyword },
            { RadAsm2Lexer.UNTIL, RadAsmTokenType.Keyword },

            { RadAsm2Lexer.PP_INCLUDE, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_DEFINE, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_UNDEF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_PRAGMA, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_ERROR, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_WARNING, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_IMPORT, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_LINE, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_INCLUDE_NEXT, RadAsmTokenType.Preprocessor },

            { RadAsm2Lexer.PP_IF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_IFDEF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_IFNDEF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_ELSE, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_ELSIF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_ELIF, RadAsmTokenType.Preprocessor },
            { RadAsm2Lexer.PP_ENDIF, RadAsmTokenType.Preprocessor },

            { RadAsm2Lexer.COMMA, RadAsmTokenType.Comma },
            { RadAsm2Lexer.SEMI, RadAsmTokenType.Semi },
            { RadAsm2Lexer.COLON, RadAsmTokenType.Colon },
            { RadAsm2Lexer.LPAREN, RadAsmTokenType.Lparen },
            { RadAsm2Lexer.RPAREN, RadAsmTokenType.Rparen },
            { RadAsm2Lexer.LSQUAREBRACKET, RadAsmTokenType.LsquareBracket },
            { RadAsm2Lexer.RSQUAREBRACKET, RadAsmTokenType.RsquareBracket },
            { RadAsm2Lexer.LCURVEBRACKET, RadAsmTokenType.LcurveBracket },
            { RadAsm2Lexer.RCURVEBRACKET, RadAsmTokenType.RcurveBracket },

            { RadAsm2Lexer.CONSTANT, RadAsmTokenType.Number },
            { RadAsm2Lexer.STRING_LITERAL, RadAsmTokenType.String },
            { RadAsm2Lexer.IDENTIFIER, RadAsmTokenType.Identifier },

            { RadAsm2Lexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsm2Lexer.EOL, RadAsmTokenType.Whitespace },

            { RadAsm2Lexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsm2Lexer.LINE_COMMENT, RadAsmTokenType.Comment },
            { RadAsm2Lexer.UNKNOWN, RadAsmTokenType.Unknown },
        };
    }
}
