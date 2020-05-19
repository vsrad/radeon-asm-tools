namespace VSRAD.SyntaxParser
{
    using Antlr4.Runtime;
    partial class RadAsmLexer : Lexer
    {
        public RadAsmLexer(string input) : this(new AntlrInputStream(input)) { }

        private bool isAt(int pos)
        {
            return _input.Index == pos;
        }
    }
}
