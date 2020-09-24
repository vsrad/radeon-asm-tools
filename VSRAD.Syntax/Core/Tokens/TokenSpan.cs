using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public struct TokenSpan
    {
        public Span Span { get; }
        public int Type { get; }

        public TokenSpan(int token, Span span) : this()
        {
            Span = span;
            Type = token;
        }
    }
}
