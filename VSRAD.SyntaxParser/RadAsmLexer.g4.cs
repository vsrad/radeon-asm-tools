namespace VSRAD.SyntaxParser
{
    using Antlr4.Runtime;
    partial class RadAsmLexer : Lexer
    {
        public RadAsmLexer(string input) : this(new AntlrInputStream(input)) { }
    }

    partial class RadAsm2Lexer : Lexer
    {
        public RadAsm2Lexer(string input) : this(new AntlrInputStream(input)) { }
    }
}
