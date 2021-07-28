namespace VSRAD.SyntaxParser
{
    using Antlr4.Runtime;
    partial class RadAsm1Lexer : Lexer
    {
        public RadAsm1Lexer(string input) : this(new AntlrInputStream(input)) { }
    }

    partial class RadAsm2Lexer : Lexer
    {
        public RadAsm2Lexer(string input) : this(new AntlrInputStream(input)) { }
    }
}
