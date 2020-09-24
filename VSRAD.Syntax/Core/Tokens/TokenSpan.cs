using Microsoft.VisualStudio.Text;
using System;

namespace VSRAD.Syntax.Core.Tokens
{
    public readonly struct TokenSpan : IEquatable<TokenSpan>
    {
        public Span Span { get; }
        public int Type { get; }

        public TokenSpan(int token, Span span) : this()
        {
            Span = span;
            Type = token;
        }

        public bool Equals(TokenSpan o) => Span == o.Span && Type == o.Type;

        public static bool operator ==(TokenSpan left, TokenSpan right) => left.Equals(right);

        public static bool operator !=(TokenSpan left, TokenSpan right) => !(left == right);

        public override bool Equals(object obj) =>  obj is TokenSpan o && Equals(o);

        public override int GetHashCode() => (Span, Type).GetHashCode();
    }
}
