using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;

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
        private LexerParser? GetLexerParser(IContentType contentType)
        {
            var instructionManager = _instructionManager.Value;

            if (contentType == _contentTypeManager.Asm1ContentType)
                return new LexerParser()
                {
                    Lexer = new AsmLexer(),
                    Parser = new Asm1Parser(this, instructionManager)
                };
            else if (contentType == _contentTypeManager.Asm2ContentType)
                return new LexerParser()
                {
                    Lexer = new Asm2Lexer(),
                    Parser = new Asm2Parser(this, instructionManager)
                };
            else if (contentType == _contentTypeManager.AsmDocContentType)
                return new LexerParser()
                {
                    Lexer = new AsmDocLexer(),
                    Parser = new AsmDocParser()
                };

            else return null;
        }
    }
}
