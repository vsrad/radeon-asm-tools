using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser.RadAsmDoc
{
    class AsmDocLexer : ILexer
    {
        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsmDocLexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsm2Lexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) => _tt[type];

        private static readonly Dictionary<int, RadAsmTokenType> _tt = new Dictionary<int, RadAsmTokenType>()
        {
            { RadAsmDocLexer.LET, RadAsmTokenType.Keyword },
            { RadAsmDocLexer.IDENTIFIER, RadAsmTokenType.Identifier },
            { RadAsmDocLexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsmDocLexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsmDocLexer.UNKNOWN, RadAsmTokenType.Unknown },
        };

        public int IDENTIFIER => RadAsmDocLexer.IDENTIFIER;
        public int LINE_COMMENT => RadAsmDocLexer.BLOCK_COMMENT;
        public int BLOCK_COMMENT => RadAsmDocLexer.BLOCK_COMMENT;
        public int LPAREN => throw new NotImplementedException();
        public int RPAREN => throw new NotImplementedException();
        public int LSQUAREBRACKET => throw new NotImplementedException();
        public int RSQUAREBRACKET => throw new NotImplementedException();
        public int LCURVEBRACKET => throw new NotImplementedException();
        public int RCURVEBRACKET => throw new NotImplementedException();
    }
}
