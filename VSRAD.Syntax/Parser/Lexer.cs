using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.IO;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser
{
    public interface ILexer
    {
        IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset);
        RadAsmTokenType LexerTokenToRadAsmToken(int type);
        int IdentifierIdx { get; }
        int LineCommentIdx { get; }
        int BlockCommentIdx { get; }
    }

    public class TextSegmentsCharStream : TextReader
    {
        private readonly IEnumerator<string> segments;
        int index;
        bool finished;

        public TextSegmentsCharStream(IEnumerable<string> segments)
        {
            this.segments = segments.GetEnumerator();
            this.segments.MoveNext();
        }

        public override int Read()
        {
            if (finished)
                return -1;
            if (index >= segments.Current.Length)
            {
                if (!segments.MoveNext())
                {
                    finished = true;
                    return -1;
                }
                index = 0;
            }
            return segments.Current[index++];
        }

        public override int Peek()
        {
            if (finished)
                return -1;
            return segments.Current[index];
        }
    }

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
            { RadAsmLexer.LBRACKET, RadAsmTokenType.Structural },
            { RadAsmLexer.RBRACKET, RadAsmTokenType.Structural },

            { RadAsmLexer.CONSTANT, RadAsmTokenType.Number },
            { RadAsmLexer.STRING_LITERAL, RadAsmTokenType.String },
            { RadAsmLexer.IDENTIFIER, RadAsmTokenType.Identifier },

            { RadAsmLexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsmLexer.EOL, RadAsmTokenType.Whitespace },

            { RadAsmLexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsmLexer.LINE_COMMENT, RadAsmTokenType.Comment },
            { RadAsmLexer.UNKNOWN, RadAsmTokenType.Unknown },
        };

        public int IdentifierIdx => RadAsmLexer.IDENTIFIER;
        public int LineCommentIdx => RadAsmLexer.LINE_COMMENT;
        public int BlockCommentIdx => RadAsmLexer.BLOCK_COMMENT;
    }

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

            { RadAsm2Lexer.COMMA, RadAsmTokenType.Structural },
            { RadAsm2Lexer.SEMI, RadAsmTokenType.Structural },
            { RadAsm2Lexer.COLON, RadAsmTokenType.Structural },
            { RadAsm2Lexer.LPAREN, RadAsmTokenType.Structural },
            { RadAsm2Lexer.RPAREN, RadAsmTokenType.Structural },
            { RadAsm2Lexer.LBRACKET, RadAsmTokenType.Structural },
            { RadAsm2Lexer.RBRACKET, RadAsmTokenType.Structural },

            { RadAsm2Lexer.CONSTANT, RadAsmTokenType.Number },
            { RadAsm2Lexer.STRING_LITERAL, RadAsmTokenType.String },
            { RadAsm2Lexer.IDENTIFIER, RadAsmTokenType.Identifier },

            { RadAsm2Lexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsm2Lexer.EOL, RadAsmTokenType.Whitespace },

            { RadAsm2Lexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsm2Lexer.LINE_COMMENT, RadAsmTokenType.Comment },
            { RadAsm2Lexer.UNKNOWN, RadAsmTokenType.Unknown },
        };

        public int IdentifierIdx => RadAsm2Lexer.IDENTIFIER;
        public int LineCommentIdx => RadAsm2Lexer.LINE_COMMENT;
        public int BlockCommentIdx => RadAsm2Lexer.BLOCK_COMMENT;
    }
}
