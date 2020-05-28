using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    public struct TokenSpan
    {
        public Span Span { get; private set; }
        public int Type { get; private set; }

        public TokenSpan(int token, Span span) : this()
        {
            Span = span;
            Type = token;
        }
    }
}
